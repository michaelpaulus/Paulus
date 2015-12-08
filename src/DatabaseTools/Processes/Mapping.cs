﻿
using DatabaseTools.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace DatabaseTools
{
	namespace Processes
	{
		public class Mapping
		{

			public static IList<Models.ColumnMappingModel> AutoMatch(IList<Models.ColumnModel> sourceTableColumns, IList<Models.ColumnModel> targetTableColumns)
			{
				List<Models.ColumnMappingModel> matchedMappings = new List<Models.ColumnMappingModel>();

				if (sourceTableColumns != null && targetTableColumns != null)
				{
					foreach (var column in sourceTableColumns)
					{
						string strColumnName = column.ColumnName;

						Models.ColumnModel targetColumn = (
						    from i in targetTableColumns
						    where i.ColumnName.EqualsIgnoreCase(strColumnName)
						    select i).FirstOrDefault();

						if (targetColumn == null)
						{
							targetColumn = (
							    from i in targetTableColumns
							    where i.ColumnName.EqualsIgnoreCase(strColumnName.Replace(" - ", "_").Replace("-", "_").Replace(" ", "_"))
							    select i).FirstOrDefault();
						}

						if (targetColumn != null && !targetColumn.IsComputed)
						{
							string strColumnMapping = string.Empty;

							if (targetColumn.DbType == System.Data.DbType.Boolean && column.DbType != System.Data.DbType.Boolean)
							{
								strColumnMapping = "%CHAR_TO_BOOLEAN%";
							}

							matchedMappings.Add(new Models.ColumnMappingModel { SourceColumnName = column.ColumnName, TargetColumnName = targetColumn.ColumnName, IsTargetColumnIdentity = targetColumn.IsIdentity, ColumnMapping = strColumnMapping});
						}
					}
				}

				return matchedMappings;
			}

			public static System.Xml.XmlAttribute CreateAttribute(System.Xml.XmlDocument doc, string name, string value)
			{
				System.Xml.XmlAttribute att = doc.CreateAttribute(name);
				att.Value = value;
				return att;
			}

			public static string CreateXml(Models.TableMapping tableMapping)
			{
				System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument();

				if (tableMapping != null && tableMapping.ColumnMappings.Count > 0)
				{

					string strSourceTableName = tableMapping.SourceTableName;
					string strTargetTableName = tableMapping.TargetTableName;

					System.Xml.XmlNode tempVar = xmlDoc.AppendChild(xmlDoc.CreateElement("table"));
					tempVar.Attributes.Append(CreateAttribute(xmlDoc, "sourceTableName", strSourceTableName));
					tempVar.Attributes.Append(CreateAttribute(xmlDoc, "targetTableName", strTargetTableName));
					foreach (Models.ColumnMappingModel mapping in tableMapping.ColumnMappings)
					{
						string strSourceColumnName = mapping.SourceColumnName;
						System.Xml.XmlNode tempVar2 = tempVar.AppendChild(xmlDoc.CreateElement("columnMapping"));
						tempVar2.Attributes.Append(CreateAttribute(xmlDoc, "sourceColumnName", mapping.SourceColumnName));
						tempVar2.Attributes.Append(CreateAttribute(xmlDoc, "targetColumnName", mapping.TargetColumnName));
						if (!(string.IsNullOrEmpty(mapping.ColumnMapping)))
						{
							tempVar2.Attributes.Append(CreateAttribute(xmlDoc, "columnMapping", mapping.ColumnMapping));
						}
					}
				}

				return xmlDoc.InnerXml;
			}

			public static string CreateScript(Models.TableMapping tableMapping)
			{
				System.Text.StringBuilder sbScript = new System.Text.StringBuilder();

				if (tableMapping != null && tableMapping.ColumnMappings.Count > 0)
				{

					bool blnHasIdentity = (
					    from i in tableMapping.ColumnMappings
					    where i.IsTargetColumnIdentity
					    select i).Count() > 0;

					string strSourceTableName = tableMapping.SourceTableName;
					string strTargetTableName = tableMapping.TargetTableName;

					if (!(string.IsNullOrEmpty(tableMapping.SourceDatabase)))
					{
						strSourceTableName = string.Concat(tableMapping.SourceDatabase, "..", strSourceTableName);
					}

					if (!(string.IsNullOrEmpty(tableMapping.TargetDatabase)))
					{
						strTargetTableName = string.Concat(tableMapping.TargetDatabase, "..", strTargetTableName);
					}

					if (blnHasIdentity)
					{
						sbScript.AppendLine(string.Format("SET IDENTITY_INSERT {0} ON;", strTargetTableName));
						sbScript.AppendLine();
					}

					System.Text.StringBuilder sbSourceColumns = new System.Text.StringBuilder();
					System.Text.StringBuilder sbTargetColumns = new System.Text.StringBuilder();

					int intColumnCount = 0;

					foreach (Models.ColumnMappingModel mapping in tableMapping.ColumnMappings)
					{
						intColumnCount += 1;

						if (sbSourceColumns.Length == 0)
						{
							sbSourceColumns.AppendFormat("{0}{0}", "\t");
						}
						else
						{
							sbSourceColumns.Append(", ");
						}

						if (intColumnCount == 4)
						{
							sbSourceColumns.AppendLine();
							sbSourceColumns.AppendFormat("{0}{0}", "\t");
						}

						sbSourceColumns.Append(mapping.SourceColumnNameWithMapping);

						if (sbTargetColumns.Length == 0)
						{
							sbTargetColumns.AppendFormat("{0}{0}", "\t");
						}
						else
						{
							sbTargetColumns.Append(", ");
						}

						if (intColumnCount == 4)
						{
							sbTargetColumns.AppendLine();
							sbTargetColumns.AppendFormat("{0}{0}", "\t");
						}

						sbTargetColumns.Append(mapping.TargetColumnNameWithMapping);

						if (intColumnCount == 4)
						{
							intColumnCount = 0;
						}
					}

					sbScript.AppendLine("INSERT INTO");
					sbScript.AppendFormat("{0}{1}", "\t", strTargetTableName);
					sbScript.AppendLine();
					sbScript.AppendFormat("{0}(", "\t");
					sbScript.AppendLine();
					sbScript.AppendLine(sbTargetColumns.ToString());
					sbScript.AppendFormat("{0})", "\t");
					sbScript.AppendLine();
					sbScript.AppendFormat("{0}(SELECT", "\t");
					sbScript.AppendLine();
					sbScript.AppendLine(sbSourceColumns.ToString());
					sbScript.AppendFormat("{0}FROM", "\t");
					sbScript.AppendLine();
					sbScript.AppendFormat("{0}{0}{1}", "\t", strSourceTableName);
					if (!(string.IsNullOrEmpty(tableMapping.SelectCriteria)))
					{
						sbScript.AppendLine();
						sbScript.AppendFormat("{0}WHERE", "\t");
						sbScript.AppendLine();
						sbScript.AppendFormat("{0}{0}{1}", "\t", tableMapping.SelectCriteria);
					}
					sbScript.AppendLine();
					sbScript.AppendFormat("{0})", "\t");
					sbScript.AppendLine();

					if (blnHasIdentity)
					{
						sbScript.AppendLine();
						sbScript.AppendLine(string.Format("SET IDENTITY_INSERT {0} OFF;", strTargetTableName));
					}

					if (tableMapping.IncludeGO)
					{
						sbScript.AppendLine("GO");
					}


				}


				return sbScript.ToString();
			}

		}
	}


}