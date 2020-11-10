using System;
using UnityEditor.Graphing;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    [Serializable]
    class CubemapMaterialSlot : MaterialSlot
    {
        public CubemapMaterialSlot()
        {}

        public CubemapMaterialSlot(
            int slotId,
            string displayName,
            string shaderOutputName,
            SlotType slotType,
            ShaderStageCapability stageCapability = ShaderStageCapability.All,
            bool hidden = false)
            : base(slotId, displayName, shaderOutputName, slotType, stageCapability, hidden)
        {}

        [SerializeField]
        internal bool m_BareTexture = false;
        internal override bool bareTexture
        {
            get { return m_BareTexture; }
            set { m_BareTexture = value; }
        }

        public override SlotValueType valueType { get { return SlotValueType.Cubemap; } }
        public override ConcreteSlotValueType concreteValueType { get { return ConcreteSlotValueType.Cubemap; } }
        public override bool isDefaultValue => true;

        public override void AddDefaultProperty(PropertyCollector properties, GenerationMode generationMode)
        {}

        public override void CopyValuesFrom(MaterialSlot foundSlot)
        {}
    }
}
