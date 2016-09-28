using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.Visitors;

namespace UnityEngine.ScriptableRenderLoop
{
	public enum PackingRules
	{
		Exact,
		Aggressive
	};

	[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
	public class GenerateHLSL : System.Attribute
	{
		public PackingRules packingRules;
		public GenerateHLSL(PackingRules rules = PackingRules.Exact)
		{
			packingRules = rules;
		}
	}

	internal class ShaderTypeGenerator
	{
		public ShaderTypeGenerator(Type type, GenerateHLSL attr)
		{
			this.type = type;
			this.attr = attr;
		}

		enum PrimitiveType
		{
			Float, Int, UInt
		};

		static string PrimitiveToString(PrimitiveType type, int rows, int cols)
		{
			string text = "";
			switch (type)
			{
				case PrimitiveType.Float:
					text = "float";
					break;
				case PrimitiveType.Int:
					text = "int";
					break;
				case PrimitiveType.UInt:
					text = "uint";
					break;
			}

			if (rows > 1)
			{
				text += rows.ToString();
				if (cols > 1)
				{
					text += "x" + cols.ToString();
				}
			}

			return text;
		}

		class Accessor
		{
			public Accessor(PrimitiveType type, string name, int rows, int cols)
			{
				this.name = name;
				this.fullType = PrimitiveToString(type, rows, cols);
				field = name;
			}

			Accessor(string name, string swizzle, string field, string fullType)
			{
				this.name = name;
				this.field = field;
				this.fullType = fullType;
			}

			public string name;
			public string field;
			public string fullType;
		};

		class ShaderFieldInfo : ICloneable
		{
			public ShaderFieldInfo(PrimitiveType type, string name, int rows, int cols)
			{
				this.type = type;
				this.name = originalName = name;
				this.rows = rows;
				this.cols = cols;
				this.comment = "";
				swizzleOffset = 0;
				packed = false;
				accessor = new Accessor(type, name, rows, cols);
			}
			public ShaderFieldInfo(PrimitiveType type, string name, int rows, int cols, string comment)
			{
				this.type = type;
				this.name = originalName = name;
				this.rows = rows;
				this.cols = cols;
				this.comment = comment;
				swizzleOffset = 0;
				packed = false;
				accessor = new Accessor(type, name, rows, cols);
			}

			public string typeString
			{
				get { return PrimitiveToString(type, rows, cols); }
			}

			public string DeclString()
			{
				return PrimitiveToString(type, rows, cols) + " " + name;
			}

			public override string ToString()
			{
				string text = DeclString() + ";";
				if (comment.Length > 0)
				{
					text += " // " + comment;
				}
				return text;
			}

			public int elementCount
			{
				get { return rows * cols; }
			}

			public object Clone()
			{
				ShaderFieldInfo info = new ShaderFieldInfo(type, name, rows, cols, comment);
				info.swizzleOffset = swizzleOffset;
				info.packed = packed;
				info.accessor = accessor;
				return info;
			}

			public PrimitiveType type;
			public string name;
			public string originalName;
			public string comment;
			public int rows;
			public int cols;
			public int swizzleOffset;
			public bool packed;
			public Accessor accessor;
		};

		void Error(string error)
		{
			if (errors == null)
			{
				errors = new List<string>();
			}
			errors.Add("Failed to generate shader type for " + type.ToString() + ": " + error);
		}

		public void PrintErrors()
		{
			if (errors != null)
			{
				foreach (var e in errors)
				{
					Debug.LogError(e);
				}
			}
		}

		void EmitPrimitiveType(PrimitiveType type, int elements, string name, string comment, List<ShaderFieldInfo> fields)
		{
			fields.Add(new ShaderFieldInfo(type, name, elements, 1, comment));
		}

		void EmitMatrixType(PrimitiveType type, int rows, int cols, string name, string comment, List<ShaderFieldInfo> fields)
		{
			fields.Add(new ShaderFieldInfo(type, name, rows, cols, comment));
		}

		bool ExtractComplex(FieldInfo field, List<ShaderFieldInfo> shaderFields)
		{
			List<FieldInfo> floatFields = new List<FieldInfo>();
			List<FieldInfo> intFields = new List<FieldInfo>();
			List<FieldInfo> uintFields = new List<FieldInfo>();
			string[] descs = new string[4] { "x: ", "y: ", "z: ", "w: " };
			int numFields = 0;

			string fieldName = "'" + field.FieldType.Name + " " + field.Name + "'";

			foreach (FieldInfo subField in field.FieldType.GetFields())
			{
				if (subField.IsStatic)
					continue;

				if (!subField.FieldType.IsPrimitive)
				{
					Error("'" + fieldName + "' can not be packed into a register, since it contains a non-primitive field type '" + subField.FieldType + "'");
					return false;
				}
				if (subField.FieldType == typeof(float))
					floatFields.Add(subField);
				else if (subField.FieldType == typeof(int))
					intFields.Add(subField);
				else if (subField.FieldType == typeof(uint))
					uintFields.Add(subField);
				else
				{
					Error("'" + fieldName + "' can not be packed into a register, since it contains an unsupported field type '" + subField.FieldType + "'");
					return false;
				}

				if (numFields == 4)
				{
					Error("'" + fieldName + "' can not be packed into a register because it contains more than 4 fields.");
					return false;
				}

				descs[numFields] += subField.Name + " ";
				numFields++;
			}
			Array.Resize(ref descs, numFields);

			string comment = string.Concat(descs);
			string mismatchErrorMsg = "'" + fieldName + "' can not be packed into a single register because it contains mixed basic types.";

			if (floatFields.Count > 0)
			{
				if (intFields.Count + uintFields.Count > 0)
				{
					Error(mismatchErrorMsg);
					return false;
				}
				EmitPrimitiveType(PrimitiveType.Float, floatFields.Count, field.Name, comment, shaderFields);
			}
			else if (intFields.Count > 0)
			{
				if (floatFields.Count + uintFields.Count > 0)
				{
					Error(mismatchErrorMsg);
					return false;
				}
				EmitPrimitiveType(PrimitiveType.Int, intFields.Count, field.Name, "", shaderFields);
			}
			else if (uintFields.Count > 0)
			{
				if (floatFields.Count + intFields.Count > 0)
				{
					Error(mismatchErrorMsg);
					return false;
				}
				EmitPrimitiveType(PrimitiveType.UInt, uintFields.Count, field.Name, "", shaderFields);
			}
			else
			{
				// Empty struct.
			}

			return true;
		}

		enum MergeResult
		{
			Merged,
			Full,
			Failed
		};

		MergeResult PackFields(ShaderFieldInfo info, ref ShaderFieldInfo merged)
		{
			if (merged.elementCount % 4 == 0)
			{
				return MergeResult.Full;
			}

			if (info.type != merged.type)
			{
				Error("can't merge '" + merged.DeclString() + "' and '" + info.DeclString() + "' into the same register because they have incompatible types.  Consider reordering the fields so that adjacent fields have the same primitive type.");
				return MergeResult.Failed;  // incompatible types
			}

			if (info.cols > 1 || merged.cols > 1)
			{
				Error("merging matrix types not yet supported ('" + merged.DeclString() + "' and '" + info.DeclString() + "').  Consider reordering the fields to place matrix-typed variables on four-component vector boundaries.");
				return MergeResult.Failed;  // don't merge matrix types
			}

			if (info.rows + merged.rows > 4)
			{
				// @TODO:  lift the restriction
				Error("can't merge '" + merged.DeclString() + "' and '" + info.DeclString() + "' because then " + info.name + " would cross register boundary.  Consider reordering the fields so that none of them cross four-component vector boundaries when packed.");
				return MergeResult.Failed;  // out of space
			}

			merged.rows += info.rows;
			merged.name += "_" + info.name;
			return MergeResult.Merged;
		}

		List<ShaderFieldInfo> Pack(List<ShaderFieldInfo> shaderFields)
		{
			List<ShaderFieldInfo> mergedFields = new List<ShaderFieldInfo>();

			List<ShaderFieldInfo>.Enumerator e = shaderFields.GetEnumerator();

			if (!e.MoveNext())
			{
				// Empty shader struct definition.
				return shaderFields;
			}

			ShaderFieldInfo current = e.Current.Clone() as ShaderFieldInfo;

			while (e.MoveNext())
			{
				while (true)
				{
					int offset = current.elementCount;
					MergeResult result = PackFields(e.Current, ref current);

					if (result == MergeResult.Failed)
					{
						return null;
					}
					else if (result == MergeResult.Full)
					{
						break;
					}

					// merge accessors
					Accessor acc = current.accessor;
					
					acc.name = current.name;
					e.Current.accessor = acc;
					e.Current.swizzleOffset += offset;

					current.packed = e.Current.packed = true;

					if (!e.MoveNext())
					{
						mergedFields.Add(current);
						return mergedFields;
					}
				}
				mergedFields.Add(current);
				current = e.Current.Clone() as ShaderFieldInfo;
			}

			return mergedFields;
		}

		public string EmitTypeDecl()
		{
			string shaderText = string.Empty;

			shaderText += "// Generated from " + type.FullName + "\n";
			shaderText += "// PackingRules = " + attr.packingRules.ToString() + "\n";
			shaderText += "struct " + type.Name + "\n";
			shaderText += "{\n";
			foreach (ShaderFieldInfo shaderFieldInfo in packedFields)
			{
				shaderText += "\t" + shaderFieldInfo.ToString() + "\n";
			}
			shaderText += "};\n";

			return shaderText;
		}

		public string EmitAccessors()
		{
			string shaderText = string.Empty;

			shaderText += "//\n";
			shaderText += "// Accessors for " + type.FullName + "\n";
			shaderText += "//\n";
			foreach (var shaderField in shaderFields)
			{
				Accessor acc = shaderField.accessor;
				string accessorName = shaderField.originalName;
				accessorName = "Get" + char.ToUpper(accessorName[0]) + accessorName.Substring(1);

				shaderText += shaderField.typeString + " " + accessorName + "(" + type.Name + " value)\n";
				shaderText += "{\n";

				string swizzle = "";

				// @TODO:  support matrix type packing?
				if (shaderField.cols == 1) // @TEMP
				{
					// don't emit redundant swizzles
					if (shaderField.originalName != acc.name)
					{
						swizzle = "." + "xyzw".Substring(shaderField.swizzleOffset, shaderField.elementCount);
					}
				}

				shaderText += "\treturn value." + acc.name + swizzle + ";\n";
				shaderText += "}\n";
			}

			return shaderText;
		}

		public string EmitDefines()
		{
			string shaderText = string.Empty;

			shaderText += "//\n";
			shaderText += "// " + type.FullName + ":  static fields\n";
			shaderText += "//\n";
			foreach (var def in statics)
			{
				shaderText += "#define " + def.Key + " (" + def.Value + ")\n";
			}

			return shaderText;
		}

		public string Emit()
		{
			return EmitDefines() + EmitTypeDecl() + EmitAccessors();
		}

		public bool Generate()
		{
			statics = new Dictionary<string, string>();

			FieldInfo[] fields = type.GetFields();
			shaderFields = new List<ShaderFieldInfo>();

			foreach (var field in fields)
			{
				if (field.IsStatic)
				{
					if (field.FieldType.IsPrimitive)
					{
						statics[field.Name] = field.GetValue(null).ToString();
					}
					continue;
				}

				if (field.FieldType.IsPrimitive)
				{
					if (field.FieldType == typeof(float))
						EmitPrimitiveType(PrimitiveType.Float, 1, field.Name, "", shaderFields);
					else if (field.FieldType == typeof(int))
						EmitPrimitiveType(PrimitiveType.Int, 1, field.Name, "", shaderFields);
					else if (field.FieldType == typeof(uint))
						EmitPrimitiveType(PrimitiveType.UInt, 1, field.Name, "", shaderFields);
					else
					{
						Error("unsupported field type '" + field.FieldType + "'");
						return false;
					}
				}
				else
				{
					// handle special types, otherwise try parsing the struct
					if (field.FieldType == typeof(Vector2))
						EmitPrimitiveType(PrimitiveType.Float, 2, field.Name, "", shaderFields);
					else if (field.FieldType == typeof(Vector3))
						EmitPrimitiveType(PrimitiveType.Float, 3, field.Name, "", shaderFields);
					else if (field.FieldType == typeof(Vector4))
						EmitPrimitiveType(PrimitiveType.Float, 4, field.Name, "", shaderFields);
					else if (field.FieldType == typeof(Matrix4x4))
						EmitMatrixType(PrimitiveType.Float, 4, 4, field.Name, "", shaderFields);
					else if (!ExtractComplex(field, shaderFields))
					{
						// Error reporting done in ExtractComplex()
						return false;
					}
				}
			}

			packedFields = shaderFields;
			if (attr.packingRules == PackingRules.Aggressive)
			{
				packedFields = Pack(shaderFields);

				if (packedFields == null)
				{
					return false;
				}
			}

			errors = null;
			return true;
		}

		public bool hasFields
		{
			get { return shaderFields.Count > 0; }
		}

		public bool hasStatics
		{
			get { return statics.Count > 0; }
		}

		public Type type;
		public GenerateHLSL attr;
		public List<string> errors = null;

		Dictionary<string, string> statics;
		List<ShaderFieldInfo> shaderFields;
		List<ShaderFieldInfo> packedFields;
	}
}
