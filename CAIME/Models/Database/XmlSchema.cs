using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace CAIME
{
    using FieldType = XmlSchemaFieldType;

    public enum XmlSchemaFieldType
    {
        YesNo,
        AutoNumber,
        Integer,
        Single,
        Double,
        Text
    }

    public class XmlSchemaField
    {
        public string       UUID;
        public string       Name;
        public int          PrimaryKey;
        public FieldType    Type;
        public bool         IsRequired;
        public int          MaxLength;
        public object       DefaultValue;
        public string       SourceColumn;
        public string       SourceTable;

        public XmlSchemaField()
        {
            IsRequired = false;
        }
    }

    public class XmlSchema
    {
        private string uuid;
        public List<XmlSchemaField> Fields { get; private set; }

        public XmlSchema()
        {
            Fields = new List<XmlSchemaField>();
        }

        public void Load(XDocument doc)
        {
            uuid = doc.Root.Element("edit_uuid").Value;

            var fieldNodes = doc.Root.Elements("field");
            foreach (var fieldNode in fieldNodes)
            {
                var field = new XmlSchemaField();
                bool failed = false;

                var nameElement         = fieldNode.Element("name");
                var fieldUUIDElement    = fieldNode.Element("field_uuid");
                var fieldTypeElement    = fieldNode.Element("field_type");
                var primaryKeyElement   = fieldNode.Element("primary_key");
                var requiredElement     = fieldNode.Element("required");
                var maxLengthElement    = fieldNode.Element("max_length");
                var defaultValueElement = fieldNode.Element("default_value");
                var sourceColumn        = fieldNode.Element("column_source_column");
                var sourceTable         = fieldNode.Element("column_source_table");

                if (nameElement != null)
                {
                    field.Name = nameElement.Value;
                }

                if (fieldUUIDElement != null)
                {
                    field.UUID = fieldUUIDElement.Value;
                }

                if (fieldTypeElement != null)
                {
                    field.Type = ParseFieldType(fieldTypeElement.Value);
                }

                if (primaryKeyElement != null)
                {
                    if (!int.TryParse(primaryKeyElement.Value, out field.PrimaryKey))
                        failed = true;
                }

                if (requiredElement != null)
                {
                    if (!int.TryParse(requiredElement.Value, out int required))
                        failed = true;
                    else
                        field.IsRequired = required == 1;
                }

                if (maxLengthElement != null)
                {
                    if (!int.TryParse(maxLengthElement.Value, out field.MaxLength))
                        failed = true;
                }

                if (defaultValueElement != null)
                {
                    field.DefaultValue = GetDefaultValue(defaultValueElement.Value, field.Type);
                }
                else
                {
                    field.DefaultValue = GetDefaultValue(null, field.Type);
                }

                if (sourceColumn != null)
                {
                    field.SourceColumn = sourceColumn.Value;
                }

                if (sourceTable != null)
                {
                    field.SourceTable = sourceTable.Value;
                }

                if (failed)
                {
                    LoggerViewModel.Log($"Corrupted table schema. Skipping {doc.DocumentType.Name}...", LogLevel.Warning);
                    return;
                }

                Fields.Add(field);
            }
        }

        private static FieldType ParseFieldType(string type)
        {
            switch (type.ToLower())
            {
                case "text":        return FieldType.Text;
                case "yesno":       return FieldType.YesNo;
                case "autonumber":  return FieldType.AutoNumber;
                case "integer":     return FieldType.Integer;
                case "single":      return FieldType.Single;
                case "double":      return FieldType.Double;
                default:            return FieldType.Text;
            }
        }
    
        public int GetFieldsCount()
        {
            return Fields.Count;
        }

        public XmlSchemaField GetField(int index)
        {
            return Fields[index];
        }

        public XmlSchemaField GetField(string name)
        {
            foreach (var field in Fields)
            {
                if (field.Name == name)
                    return field;
            }
            return null;
        }

        public void GetColumns(out string[] columnNames)
        {
            columnNames = new string[Fields.Count];
            for (int i = 0; i < columnNames.Length; ++i)
            {
                columnNames[i] = Fields[i].Name;
            }
        }

        public static Type GetFieldType(XmlSchemaField field)
        {
            switch (field.Type)
            {
                case FieldType.YesNo:
                    return typeof(bool);
                case FieldType.Text:
                    return typeof(string);
                case FieldType.AutoNumber:
                case FieldType.Integer:
                    return typeof(int);
                case FieldType.Single:
                    return typeof(float);
                case FieldType.Double:
                    return typeof(double);
            }
            return null;
        }

        private static object GetDefaultValue(string value, FieldType type)
        {
            if (value == null)
            {
                switch (type)
                {
                    case FieldType.YesNo:
                        return true;
                    case FieldType.Text:
                        return string.Empty;
                    case FieldType.Integer:
                        return 0;
                    case FieldType.Single:
                        return 0.0f;
                    case FieldType.Double:
                        return 0.0;
                    case FieldType.AutoNumber:
                        return 0;
                }
            }

            switch (type)
            {
                case FieldType.YesNo:
                    return value == "1";
                case FieldType.Text:
                    return value ?? "";
                case FieldType.Integer:
                    if (!int.TryParse(value, out int intValue))
                    {
                        return 0;
                    }
                    return intValue;
                case FieldType.Single:
                    if (!float.TryParse(value, out float floatValue))
                    {
                        return 0.0f;
                    }
                    return floatValue;
                case FieldType.Double:
                    if (!double.TryParse(value, out double doubleValue))
                    {
                        return 0.0;
                    }
                    return doubleValue;
                case FieldType.AutoNumber:
                    return 0;
            }

            return null;
        }
    }
}
