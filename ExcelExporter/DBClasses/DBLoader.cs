using System.Diagnostics;
using System.Data;
using System.Text;
using System.Xml;
using System.IO;
using System;

namespace ExcelExporter
{
   public class DBLoader
   {
      private string _strApplicationPath;
      private DatabaseInfo _ActualDatabase = new DatabaseInfo();


      public DBLoader()
      {
      }


      public string ApplicationPath
      {
         get { return _strApplicationPath; }
         set
         {
            _strApplicationPath = value;
         }
      }


      public void ProgressStart(string strAction, string strPath, string strKey)
      {
         switch (strAction)
         {
            case "Extract":
               _ActualDatabase = new DatabaseInfo(strPath, strKey);
               this.ImportDatabase();
               break;
            case "Build":
               _ActualDatabase = new DatabaseInfo(strPath, strKey);
               this.ExportDatabase();
               break;
            default:
               return;
         }
      }


      private bool ImportDatabase()
      {
         // Export aus CDB
         Console.WriteLine("Extracting data");
         string strExportPath = string.Concat(DatabaseFolder, Const.DumpOutFileName);
         ProcessStartInfo pci = new ProcessStartInfo(string.Concat(_strApplicationPath, Const.ExporterApplication));
         pci.Arguments = string.Format(" -input \"{0}\" -output \"{1}\" -ToXML", _ActualDatabase.Path, strExportPath);
         pci.WindowStyle = ProcessWindowStyle.Hidden;//hide console 
         Process proc = Process.Start(pci);
         int intHlp = 0;
         while (!proc.HasExited) 
            if (intHlp++ > 9000000)
               return false;
         //Daten laden
         Console.WriteLine("Reading data");
         StringBuilder strData = new StringBuilder(100);
         XmlDocument xdocSource = new XmlDocument();
         xdocSource.Load(strExportPath);
         //testen, ob das File korrekt ist
         XmlNodeList xNodeList = xdocSource.SelectNodes(Const.db);
         if (xNodeList.Count != 1)
         {
            if (xNodeList.Count == 0)
               Console.WriteLine("Wrong file");
            return false;
         }
         //Basic-Infos laden
         XmlNode xDatabase = xNodeList.Item(0);
         int intNumOrigTables = int.Parse(xDatabase.Attributes[Const.db_NumOTables].InnerXml);
         int intNumTables = int.Parse(xDatabase.Attributes[Const.db_NumTables].InnerXml);
         //Tabellen anlegen
         DBTable[] dbtTables;
         dbtTables = new DBTable[intNumTables];
         //Loop über alle Tabellen
         int intTableIndex = -1;
         xNodeList = xDatabase.ChildNodes;
         foreach (XmlNode xTable in xNodeList)
         {
            intTableIndex ++;
            string strTableName = xTable.Attributes[Const.table_name].InnerXml;
            Console.WriteLine(string.Concat("Reading ", strTableName));
            int intTableId = int.Parse(xTable.Attributes[Const.table_id].InnerXml);
            int intNumRows = int.Parse(xTable.Attributes[Const.table_NumRows].InnerXml);
            int intNumCols = int.Parse(xTable.Attributes[Const.table_NumCols].InnerXml);
            int intNumOrigCols = int.Parse(xTable.Attributes[Const.table_NumOCols].InnerXml);
            string[,] strCells = new String[intNumRows, intNumCols];
            string[] strColumns = new String[intNumCols];
            //preprocess the columns, to know the DB structure 
            int intColIndex = -1;
            foreach (XmlNode xnColumn in xTable.ChildNodes)
            {
               intColIndex++;
               string strDataType = xnColumn.Attributes[Const.column_type].Value;
               string strColumnName = xnColumn.Attributes[Const.column_name].Value;
               int intColumnId = int.Parse(xnColumn.Attributes[Const.column_ID].Value);
               strColumns[intColIndex] = strColumnName;
               int intRowIndex = -1;
               switch (strDataType)
               {
                  case "ListInt":
                  case "ListFloat":
                     foreach (XmlNode xnList in xnColumn.ChildNodes)
                     {
                        intRowIndex++;
                        if (int.Parse(xnList.Attributes[Const.list_size].Value) == 0)
                           strCells[intRowIndex, intColIndex] = "()";
                        else
                        {
                           strData.Length = 0;
                           foreach (XmlNode xItem in xnList.ChildNodes)
                              strData.Append("," + xItem.InnerText);
                           if (strData.Length > 0)
                              strCells[intRowIndex, intColIndex] = '(' + strData.Remove(0, 1).ToString() + ')';
                           else
                              strCells[intRowIndex, intColIndex] = "()";
                        }
                     }
                     break;
                  default:
                     foreach (XmlNode xnCell in xnColumn.ChildNodes)
                        strCells[++intRowIndex, intColIndex] = xnCell.InnerText;
                     break;
               }
            }
            // Tabelle mit allen Daten erstellen (inkl. Hilfsspalte)
            dbtTables[intTableIndex] = new DBTable(strTableName, intTableId, intNumOrigCols, intNumCols, intNumRows, strColumns, strCells);
         }

         // Datenbank-Informationen und Daten speichern
         Console.WriteLine("Writing files");
         XmlDocument xdocSettings = new XmlDocument();
         intNumTables = 0;
         XmlNode xRoot = xdocSettings.CreateElement(Const.TopNode);
         XmlNode xTables = xdocSettings.CreateElement(Const.SettingsTables);
         //Alle Tabellen speichern
         foreach (DBTable t in dbtTables)
         {
            if (t != null)
            {
               intNumTables++;
               t.WriteXml(string.Concat(DatabaseFolder, t.TableName, ".xml"));
               t.WriteXmlSchema(string.Concat(DatabaseFolder, t.TableName, ".xsd"));
               XmlNode xTable = xdocSettings.CreateElement(Const.SettingsTable);
               xTable.Attributes.Append(xdocSettings.CreateAttribute(Const.SettingsTableName)).InnerText = t.TableName;
               xTables.AppendChild(xTable);
            }
         }
         // Anzahl + Namen der Tabellen speichern
         xTables.Attributes.Append(xdocSettings.CreateAttribute(Const.db_NumTables)).InnerText = intNumTables.ToString();
         xRoot.AppendChild(xTables);
         // Speichern in Settings.xml
         xdocSettings.AppendChild(xRoot);
         xdocSettings.Save(string.Concat(DatabaseFolder, Const.SettingsFileName));

         return true;
      }


      //This function simply rewrites the export.xml file 
      //It loads the former export.xml to copy the structure and add the data 
      private bool ExportDatabase() 
      {
         string strPathSettings = string.Concat(DatabaseFolder, Const.SettingsFileName);
         // DB Informationen einlesen
         Console.WriteLine("Load data");
         XmlDocument xdocSettings = new XmlDocument();
         xdocSettings.Load(strPathSettings);
         XmlNode xRoot = xdocSettings.SelectSingleNode(string.Concat("/", Const.TopNode));
         XmlNode xTables = xRoot.SelectSingleNode(Const.SettingsTables);
         int intNumTables = int.Parse(xTables.Attributes[Const.db_NumTables].InnerXml);
         DBTable[] dbtTables = new DBTable[intNumTables];
         XmlNodeList xNodeList = xTables.ChildNodes;
         intNumTables = 0;
         foreach (XmlNode xTable in xNodeList)
         {
            string strTableName = xTable.Attributes[Const.SettingsTableName].InnerXml;
            dbtTables[intNumTables] = new DBTable();
            dbtTables[intNumTables].ReadXmlSchema(string.Concat(DatabaseFolder, strTableName, ".xsd"));
            dbtTables[intNumTables].ReadXml(string.Concat(DatabaseFolder, strTableName, ".xml"));
            // Es sollte zwar nie vorkommen, aber falls im Schema eine Relation definiert ist, wird automatisch ein Dataset erstellt. >> Fehler in späterem Code, weil Tabelle nur in einem Dataset vorkommen darf
            if (dbtTables[intNumTables].DataSet != null)
            {
               DataSet ds = dbtTables[intNumTables].DataSet;
               while (ds.Relations.Count > 0)
                  ds.Relations.Remove(ds.Relations[0]);
               ds.Tables.Remove(dbtTables[intNumTables]);
               ds = null;
            }
            intNumTables++;
         }

         char[] chrTrim = { '(', ')' };
         Console.WriteLine("Prepare file");
         XmlDocument xdocDumpOut = new XmlDocument();
         xdocDumpOut.Load(string.Concat(DatabaseFolder, Const.DumpOutFileName));
         XmlDocument xdocDumpIn = new XmlDocument();
         xdocDumpIn.AppendChild(xdocDumpIn.ImportNode(xdocDumpOut.FirstChild, false));
         XmlNode xDumpOutRoot = xdocDumpOut.FirstChild;
         XmlNode DBdest = xdocDumpIn.FirstChild;

         for (int intTableIndex = 0; intTableIndex < xDumpOutRoot.ChildNodes.Count; ++intTableIndex)
         {
            //
            XmlNode xDumpOutTable = xDumpOutRoot.ChildNodes.Item(intTableIndex);
            string strTableName = xDumpOutTable.Attributes[Const.table_name].Value;
            Console.WriteLine(string.Concat("Writing ", strTableName));
            //
            XmlElement xDumpInTable;
            DataView dvTable = new DataView();
            int intIndex = 0;
            while (strTableName != dbtTables[intIndex].TableName)
               intIndex++;
            if (strTableName == dbtTables[intIndex].TableName)
            {
               dvTable = new DataView(dbtTables[intIndex]);
               int intNumRows = dvTable.Count;
               xDumpInTable = xdocDumpIn.CreateElement(Const.table);
               XmlAttribute xAttribute;
               xAttribute = xdocDumpIn.CreateAttribute(Const.table_name);
               xAttribute.Value = strTableName;
               xDumpInTable.SetAttributeNode(xAttribute);
               xAttribute = xdocDumpIn.CreateAttribute(Const.table_id);
               xAttribute.Value = xDumpOutTable.Attributes[Const.table_id].Value;
               xDumpInTable.SetAttributeNode(xAttribute);
               xAttribute = xdocDumpIn.CreateAttribute(Const.table_NumOCols);
               xAttribute.Value = xDumpOutTable.Attributes[Const.table_NumOCols].Value;
               xDumpInTable.SetAttributeNode(xAttribute);
               xAttribute = xdocDumpIn.CreateAttribute(Const.table_NumCols);
               xAttribute.Value = xDumpOutTable.Attributes[Const.table_NumCols].Value;
               xDumpInTable.SetAttributeNode(xAttribute);
               xAttribute = xdocDumpIn.CreateAttribute(Const.table_NumRows);
               xAttribute.Value = intNumRows.ToString();
               xDumpInTable.SetAttributeNode(xAttribute);

               foreach (XmlNode xDumpOutColumn in xDumpOutTable.ChildNodes)
               {
                  XmlElement xDumpInColumn = xdocDumpIn.CreateElement(Const.column);
                  string strColumnName = xDumpOutColumn.Attributes[Const.column_name].Value;
                  string strColumnType = xDumpOutColumn.Attributes[Const.column_type].Value;
                  xAttribute = xdocDumpIn.CreateAttribute(Const.column_name);
                  xAttribute.Value = strColumnName;
                  xDumpInColumn.SetAttributeNode(xAttribute);
                  xAttribute = xdocDumpIn.CreateAttribute(Const.column_ID);
                  xAttribute.Value = xDumpOutColumn.Attributes[Const.column_ID].Value;
                  xDumpInColumn.SetAttributeNode(xAttribute);
                  xAttribute = xdocDumpIn.CreateAttribute(Const.column_type);
                  xAttribute.Value = strColumnType;
                  xDumpInColumn.SetAttributeNode(xAttribute);

                  for (int i = 0; i < intNumRows; ++i)
                  {
                     XmlElement xCell;
                     string strValue = dvTable[i][strColumnName].ToString();
                     switch (strColumnType)
                     {
                        case "ListInt":
                        case "ListFloat":
                           xCell = xdocDumpIn.CreateElement(Const.list);
                           strValue = strValue.Trim(chrTrim); // Dieser Code ist besser, denn es kann ja sein, dass gar keine Klammern vorhanden sind
                           if (string.IsNullOrEmpty(strValue))
                           {
                              XmlAttribute xListSize = xdocDumpIn.CreateAttribute(Const.list_size);
                              xListSize.Value = "0";
                              xCell.SetAttributeNode(xListSize);
                           }
                           else
                           {
                              string[] items = strValue.Split(',');
                              foreach (string item in items)
                              {
                                 XmlElement xItem = xdocDumpIn.CreateElement(Const.cell);
                                 if (!string.IsNullOrEmpty(item))
                                    xItem.InnerText = item;
                                 xCell.InsertAfter(xItem, xCell.LastChild);
                              }
                              XmlAttribute xListSize = xdocDumpIn.CreateAttribute(Const.list_size);
                              xListSize.Value = items.Length.ToString();
                              xCell.SetAttributeNode(xListSize);
                           }
                           break;
                        default: // normal cell, just copy the data 
                           xCell = xdocDumpIn.CreateElement(Const.cell);
                           if (!string.IsNullOrEmpty(strValue))
                              xCell.InnerText = strValue; //SecurityElement.Escape(strValue)
                           break;
                     }
                     xDumpInColumn.InsertAfter(xCell, xDumpInColumn.LastChild);
                  }
                  xDumpInTable.InsertAfter(xDumpInColumn, xDumpInTable.LastChild);
               }
               DBdest.InsertAfter(xDumpInTable, DBdest.LastChild);
            }
         }
         string strExportPath = string.Concat(DatabaseFolder, Const.DumpInFileName);
         xdocDumpIn.Save(strExportPath);
         // Import in die CDB
         Console.WriteLine("Building database");
         ProcessStartInfo pci = new ProcessStartInfo(string.Concat(_strApplicationPath, Const.ExporterApplication));
         pci.Arguments = string.Format(" -input \"{0}\" -output \"{1}\" -FromXML", strExportPath, _ActualDatabase.Path);
         pci.WindowStyle = ProcessWindowStyle.Hidden;//hide console 
         Process proc = Process.Start(pci);
         int intHlp = 0;
         while (!proc.HasExited)
            if (intHlp++ > 9000000)
               return false;

         return true;
      }


      private string DatabaseFolder
      {
         get
         {
            string strPath = string.Concat(_strApplicationPath, "Data\\", _ActualDatabase.Key, "\\");
            if (!Directory.Exists(strPath))
               Directory.CreateDirectory(strPath);
            return strPath;
         }
      }

   }


   public class DatabaseInfo
   {
      public string Path;
      public string Key;
      public DatabaseInfo() { }
      public DatabaseInfo(string strPath, string strKey)
      {
         this.Path = strPath;
         this.Key = strKey;
      }
   }

}
