using System.Collections.Generic;
using System.Data;
using System.Xml;
using System.IO;
using System;

namespace FoolEditor
{
   class DBDataset : DataSet
   {
      private int _intIDteam = 0;
      private int _intPCMVersion = -1;
      private bool _blnGridLayoutIsLoaded = false;
      private List<string> _strTableList = new List<string>();
      private DBTable _tblHlpLayout = null;
      private string _strHlpTable = null;


      public DBDataset(DBTable[] dbtTables)
      {
         // Alle geladenen Tabellen ins Dataset übernehmen
         foreach (DBTable t in dbtTables)
            if (t != null)
               base.Tables.Add(t);
         // Hilfstabelle für Datenfehler (und Andere)
#if DEBUG
         DBTable tblError = new DBTable("DatabaseErrors", 0, 0, 3, 0);
         tblError.ColumnAdd(0, 0, "IDerror", "Int32");
         tblError.ColumnAdd(1, 1, "Type", "String");
         tblError.ColumnAdd(2, 2, "Error", "String");
         base.Tables.Add(tblError);
#endif
         // Prüfen, ob DB aus PCM11, PCM10, PCM09 oder PCM08 (Information wird momentan nur hier verwendet)
         _intPCMVersion = -1;
         try
         { // Tabelle existiert erst in PCM2011
            base.Tables["DYN_businessman"].Columns["IDbusinessman"].ToString(); 
            _intPCMVersion = 11; 
         } 
         catch 
         {
            try { base.Tables["DYN_contract_manager"].Columns["IDxchange_manager"].ToString(); } // Tabelle existiert nicht mehr in PCM2010
            catch { _intPCMVersion = 10; }
            if (_intPCMVersion == -1)
            {
               _intPCMVersion = 9;
               try { base.Tables["STA_type_rider"].Columns["f_acceleration_ratio"].ToString(); } // Feld existiert noch nicht in PCM2008
               catch { _intPCMVersion = 8; }
            }
         }
         // Team vom Spieler auslesen
         DataView dv;
         switch (_intPCMVersion)
         { 
            case 10:
            case 11:
               dv = new DataView(base.Tables["DYN_manager"]);
               break;
            default:
               dv = new DataView(base.Tables["DYN_contract_manager"]);
               break;
         }
         try { _intIDteam = int.Parse(dv[0][Const.ColumnName_TeamFK].ToString()); }
         catch { }
         // Tabellen verknüpfen, damit diese auf dem Interface angezeigt werden -> Untertabelle im DataGrid
#if DEBUG
#if TestDatabase 
         string strDatabaseErrorList = "";
         string strDatatypeErrorList = "";
#endif
         string strDataErrorList = "";
         int intRowIndex = 0;
#endif
         foreach (DBTable t in base.Tables)
         {
            foreach (string strFK in t.ForeignKeyList)
            {
#if DEBUG
               string strError = "";
#endif
               DBTable tblMaster = null;
               DataRelation dRelation = null;
               string strHlpPK = strFK.Substring(2);
               try
               {
                  if (strHlpPK == "IDinjury")
                  {
                     switch (t.TableName)
                     {
                        case "DYN_cyclist":
                           tblMaster = (DBTable)base.Tables["DYN_injury"];
                           break;
                        case "DYN_injury":
                           tblMaster = (DBTable)base.Tables["STA_injury"];
                           break;
                        default:
                           tblMaster = this.GetTable(strHlpPK);
                           break;
                     }
                  }
                  else
                     tblMaster = this.GetTable(strHlpPK);
                  if (tblMaster != null)
                  {
                     if (tblMaster.TableName != t.TableName)
                     {
                        DataColumn dc1 = tblMaster.PrimaryKey;
                        DataColumn dc2 = t.Columns[strFK];
                        if (dc1.DataType == dc2.DataType)
                        {
#if DEBUG
                           strError = t.TableName + " (" + strFK + ") >> " + tblMaster.TableName + " (" + tblMaster.PrimaryKeyName + ")";
#endif
                           dRelation = new DataRelation(string.Concat(t.TableName, " linked to ", tblMaster.PrimaryKeyName), dc1, dc2);
                           base.Relations.Add(dRelation);
                        }
#if TestDatabase
                        else
                           strDatatypeErrorList = strDatatypeErrorList + Environment.NewLine + "Link not possible because of wrong datatype: " + tblMaster.TableName + "." + tblMaster.PrimaryKeyName + " > " + t.TableName + "." + strFK;
#endif
                     }
                  }
#if TestDatabase
                  else
                     strDatabaseErrorList = strDatabaseErrorList + Environment.NewLine + "No table found with primary key " + strHlpPK + " (Linked in " + t.TableName + ")";
#endif
               }
#if !DEBUG
               catch (ArgumentException)
               {
                  if (tblMaster != null)
                  {
                     if (tblMaster.Rows.Count > 0)
                     {
                        object[] objRowValues = new object[tblMaster.Columns.Count];
                        foreach (DataColumn c in tblMaster.Columns)
                        {
                           if (c.ColumnName == tblMaster.PrimaryKeyName)
                              objRowValues[c.Ordinal] = 0;
                           else if (c.ColumnName == Const.ColumnName_IsHelpRow)
                              objRowValues[c.Ordinal] = true;
                           else
                              switch (c.DataType.ToString())
                              {
                                 case "System.String":
                                    objRowValues[c.Ordinal] = "DUMMY - DO NOT EDIT OR DELETE THIS ROW";
                                    break;
                                 case "System.Boolean":
                                    objRowValues[c.Ordinal] = false;
                                    break;
                                 case "Bool":
                                    break;
                                 default:
                                    objRowValues[c.Ordinal] = 0;
                                    break;
                              }
                        }
                        try
                        {
                           tblMaster.Rows.Add(objRowValues);
                           base.Relations.Remove(dRelation);
                           base.Relations.Add(dRelation);
                        }
                        catch { }
                     }
                  }
               }
#endif
#if DEBUG
               catch (Exception e)
               {
                  if (!string.IsNullOrEmpty(strError))
                  {
                     strDataErrorList = strDataErrorList + e.Message + ": " + strError + Environment.NewLine + Environment.NewLine;
                     tblError.Rows.Add();
                     tblError.SetValue(intRowIndex, 0, intRowIndex);
                     tblError.SetValue(intRowIndex, 1, e.Message);
                     tblError.SetValue(intRowIndex++, 2, strError);
                  }
               }
#endif
            }
         }
#if DEBUG
         StreamWriter sw;
#if TestDatabase
         sw = new StreamWriter(DBLoader.ApplicationPath + "Database_Error.txt");
         sw.Write(strDatatypeErrorList + strDatabaseErrorList);
         sw.Close();
         sw.Dispose();
#endif
         sw = new StreamWriter(DBLoader.ApplicationPath + "Data_Error.txt");
         sw.Write(DBLoader.LoadedDatabaseName + strDataErrorList);
         sw.Close();
         sw.Dispose();
#endif
      }


      public DBTable GetTable(string strPrimaryKey)
      {
         foreach (DBTable t in base.Tables)
            if (t.PrimaryKeyName == strPrimaryKey)
               return t;
         return null;
      }

      public string GetTableName(string strPrimaryKey)
      {
         if (strPrimaryKey != "IDinjury")
            return this.SearchTableName(strPrimaryKey);
         return null;
      }


      public int ManagedTeamID
      {
         get { return _intIDteam; }
      }


      public DataView GetDataView(string strTableName, bool blnUseTeamFilter)
      {
         DataView dv = new DataView(base.Tables[strTableName]);
         if (blnUseTeamFilter && (_intIDteam > 0))
         {
            try { dv.RowFilter = string.Format("{0}={1} AND {2}=false", Const.ColumnName_TeamFK, _intIDteam, Const.ColumnName_IsHelpRow); }
            catch { dv.RowFilter = string.Format("{0}=false", Const.ColumnName_IsHelpRow); }
         }
         else
            dv.RowFilter = string.Format("{0}=false", Const.ColumnName_IsHelpRow);
         return dv;
      }


      public void SplitFieldLayouts(string strFieldLayouts, string strTable)
      {
         XmlDocument xdoc = new XmlDocument();
         xdoc.LoadXml(strFieldLayouts);
         //xdoc.Save(string.Concat(DBLoader.ApplicationPath, "save.xml"));
         XmlNode xLayouts = xdoc.SelectSingleNode("/xamDataPresenter/fieldLayouts");
         XmlNodeList xLayoutList = xLayouts.ChildNodes;
         if (string.IsNullOrEmpty(strTable))
            foreach (XmlNode xLayout in xLayoutList)
            {
               strTable = xLayout.Attributes["key"].InnerXml;
               try
               {
                  DBTable t = (DBTable)base.Tables[strTable];
                  if (t != null)
                     t.FieldLayout = xLayout;
               }
               catch { }
            }
         else
            foreach (XmlNode xLayout in xLayoutList)
               if (strTable == xLayout.Attributes["key"].InnerXml)
               {
                  try
                  {
                     DBTable t = (DBTable)base.Tables[strTable];
                     if (t != null)
                        t.FieldLayout = xLayout;
                  }
                  catch { }
                  return;
               }
      }

      public string BuildFieldLayouts(List<string> strTableList)
      {
         _strTableList = strTableList;
         if (!_blnGridLayoutIsLoaded)
            this.LoadFieldLayouts();
         return this.BuildFieldLayouts();
      }

      public void SaveFieldLayouts()
      {
         if (!_blnGridLayoutIsLoaded) return;
         XmlDocument xdoc = new XmlDocument();
         XmlNode xRoot = xdoc.CreateElement("xamDataPresenter");
         xRoot.Attributes.Append(xdoc.CreateAttribute("version")).InnerText = "8.2.20082.2001";
         XmlNode xLayouts = xdoc.CreateElement("fieldLayouts");
         foreach (DBTable t in base.Tables)
            if (t.FieldLayout != null)
               xLayouts.AppendChild(xdoc.ImportNode(t.FieldLayout, true));
         xRoot.AppendChild(xLayouts);
         xdoc.AppendChild(xRoot);
         xdoc.Save(string.Format("{0}PCM{1:00}_GridLayout.xml", DBLoader.FieldLayoutsPath, _intPCMVersion));
      }


      public DataView GetDataViewFieldLayout(string strTable)
      {
         DBTable t = (DBTable)base.Tables[strTable];
         if (t.FieldLayout == null)
            return null;

         _strHlpTable = strTable;
         _tblHlpLayout = new DBTable(Const.TableName_FieldLayout, 0, 0, 5, 0);
         _tblHlpLayout.ColumnAdd(0, 0, "Idxno", "Int32");
         _tblHlpLayout.ColumnAdd(1, 1, "FieldName", "String");
         _tblHlpLayout.ColumnAdd(2, 2, "Width", "Int32");
         _tblHlpLayout.ColumnAdd(3, 3, "Column", "Int32");
         _tblHlpLayout.ColumnAdd(4, 4, "LayoutGroups", "String");

         XmlNodeList xFieldList = t.FieldLayout.SelectSingleNode("fields").ChildNodes;
         int intColIndex = 0;
         bool blnLastFieldReached = false;
         foreach (XmlNode xField in xFieldList)
         {
            string strFieldName = xField.Attributes["name"].InnerXml;
            if (strFieldName == Const.ColumnName_IsHelpRow) 
               blnLastFieldReached = true;
            if (!blnLastFieldReached)
            {
               object[] objRowValues = new object[5];
               objRowValues[0] = intColIndex;
               objRowValues[1] = strFieldName;
               objRowValues[2] = this.ValidatedAttributeValue(xField, "cellWidth");
               objRowValues[3] = this.ValidatedAttributeValue(xField, "column", intColIndex);
               objRowValues[4] = "0";
               _tblHlpLayout.Rows.Add(objRowValues);
            }
            intColIndex++;
         }
         return new DataView(_tblHlpLayout);
      }

      public void ApplyFieldLayoutDefinition()
      {
         DataView dv = new DataView(_tblHlpLayout);
         dv.Sort = "Idxno ASC";
         XmlDocument xdoc = new XmlDocument();
         DBTable t = (DBTable)base.Tables[_strHlpTable];
         XmlNode xLayout = t.FieldLayout;
         XmlNode xFields = xLayout.SelectSingleNode("fields");
         XmlNodeList xFieldList = xFields.ChildNodes;
         for (int i = 0; i < dv.Count; i++)
         {
            Int32 intValue = -1;
            try { intValue = (Int32)dv[i]["Width"]; }
            catch { }
            if (intValue >= 0)
            {
               ((XmlElement)xFieldList[i]).SetAttribute("cellWidth", intValue.ToString());
               ((XmlElement)xFieldList[i]).SetAttribute("labelWidth", intValue.ToString());
            }
            else
            {
               ((XmlElement)xFieldList[i]).RemoveAttribute("cellWidth");
               ((XmlElement)xFieldList[i]).RemoveAttribute("labelWidth");
            }
            string strValue = dv[i]["Column"].ToString();
            ((XmlElement)xFieldList[i]).SetAttribute("column", strValue);
            ((XmlElement)xFieldList[i]).SetAttribute("row", "0");
            ((XmlElement)xFieldList[i]).SetAttribute("rowSpan", "1");
            ((XmlElement)xFieldList[i]).SetAttribute("columnSpan", "1");
         }
      }


      private string BuildFieldLayouts()
      {
         XmlDocument xdoc = new XmlDocument();
         XmlNode xRoot = xdoc.CreateElement("xamDataPresenter");
         xRoot.Attributes.Append(xdoc.CreateAttribute("version")).InnerText = "8.2.20082.2001";
         XmlNode xLayouts = xdoc.CreateElement("fieldLayouts");
         foreach (string s in _strTableList)
         {
            DBTable t = (DBTable)base.Tables[s];
            if (t.FieldLayout != null)
               xLayouts.AppendChild(xdoc.ImportNode(t.FieldLayout, true));
         }
         xRoot.AppendChild(xLayouts);
         xdoc.AppendChild(xRoot);
         return xdoc.OuterXml;
      }

      private void LoadFieldLayouts()
      {
         _blnGridLayoutIsLoaded = true;
         string strPath = string.Format("{0}PCM{1:00}_GridLayout.xml", DBLoader.FieldLayoutsPath, _intPCMVersion);
         if (File.Exists(strPath))
         {
            XmlDocument xdoc = new XmlDocument();
            xdoc.Load(strPath);
            this.SplitFieldLayouts(xdoc.OuterXml, null);
         }
      }


      private string SearchTableName(string strPrimaryKey)
      {
         foreach (DBTable t in base.Tables)
            if (t.PrimaryKeyName == strPrimaryKey)
               return t.TableName;
         return null;
      }


      private Int32 ValidatedAttributeValue(XmlNode xField, string strAttribute)
      {
         return this.ValidatedAttributeValue(xField, strAttribute, -1);
      }
      private Int32 ValidatedAttributeValue(XmlNode xField, string strAttribute, Int32 intDefault)
      {
         try { return Int32.Parse(xField.Attributes[strAttribute].InnerXml); }
         catch { return intDefault; }
      }

   }
}
