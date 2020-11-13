using System;
using UnityEditor.Graphing;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    [Serializable]
    class Texture2DArrayMaterialSlot : MaterialSlot
    {
        public Texture2DArrayMaterialSlot()
        {}

        public Texture2DArrayMaterialSlot(
            int slotId,
            string displayName,
            string shaderOutputName,
            SlotType slotType,
            ShaderStageCapability shaderStageCapability = ShaderStageCapability.All,
            bool hidden = false)
            : base(slotId, displayName, shaderOutputName, slotType, shaderStageCapability, hidden)
        {}

        [SerializeField]
        internal bool m_BareResource = false;
        internal override bool bareResource
        {
            get { return m_BareResource; }
            set { m_BareResource = value; }
        }

        public override string GetHLSLVariableType()
        {
            if (m_BareResource)
                return "Texture2DArray";
            else
                return concreteValueType.ToShaderString();
        }

        public override SlotValueType valueType { get { return SlotValueType.Texture2DArray; } }
        public override ConcreteSlotValueType concreteValueType { get { return ConcreteSlotValueType.Texture2DArray; } }
        public override bool isDefaultValue => true;

        public override void AddDefaultProperty(PropertyCollector properties, GenerationMode generationMode)
        {}

        public override void CopyValuesFrom(MaterialSlot foundSlot)
        {
            var slot = foundSlot as Texture2DArrayMaterialSlot;
            if (slot != null)
            {
                m_BareResource = slot.m_BareResource;
            }
        }
    }
}
