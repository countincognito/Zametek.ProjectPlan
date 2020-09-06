using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace Zametek.View.ProjectPlan
{
    public class LocalizedDescriptionAttribute
        : DescriptionAttribute
    {
        #region Fields

        private ResourceManager m_ResourceManager;
        private string m_ResourceKey;

        #endregion

        #region Ctors

        public LocalizedDescriptionAttribute(string resourceKey, Type resourceType)
        {
            m_ResourceManager = new ResourceManager(resourceType);
            m_ResourceKey = resourceKey;
        }

        #endregion

        #region Overrides

        public override string Description
        {
            get
            {
                string description = m_ResourceManager.GetString(m_ResourceKey, CultureInfo.InvariantCulture);
                return string.IsNullOrWhiteSpace(description) ? string.Format(CultureInfo.InvariantCulture, "[[{0}]]", m_ResourceKey) : description;
            }
        }

        #endregion
    }
}
