using System.Collections.Generic;
using System.Data;
using System.Xml;
using System;

namespace ExcelExporter
{
   public class DBTable : DataTable
   {
      public enum DataType { Normal, Bool, Float, List };

      private DBColumn[] _dbcColumns;
      private XmlNode _xLayout = null;
      private string _strPrimaryKey = null;
      private List<string> _strForeignKeyList = new List<string>();


      #region PUBLIC

      public DBTable(string strTableName, string strPrimaryKey, string strForeignKeyList) : base(strTableName)
      {
         _strPrimaryKey = strPrimaryKey;
         if (!string.IsNullOrEmpty(strForeignKeyList))
         {
            string[] strList = strForeignKeyList.Split(',');
            foreach (string s in strList)
               _strForeignKeyList.Add(s);
         }
      }

      public DBTable(string strTableName, int intTableId, int intNumOrigCols, int intNumCols, int intNumRows) : base(strTableName)
      {
         _dbcColumns = new DBColumn[intNumCols + 1];
      }

      new public DataColumn PrimaryKey
      {
         get { return base.Columns[_strPrimaryKey]; }
      }

      public string PrimaryKeyName
      {
         get { return _strPrimaryKey; }
      }

      public List<string> ForeignKeyList
      {
         get { return _strForeignKeyList; }
      }

      public string ForeignKeyListAsString
      {
         get 
         {
            string strList = "";
            foreach (string s in _strForeignKeyList)
            {
               strList = string.Concat(strList, ',', s);
            }
            if (strList.Length > 1)
               return strList.Substring(1);
            return strList;
         }
      }

      public XmlNode FieldLayout
      {
         get { return _xLayout; }
         set { _xLayout = value; }
      }


      #region Column

      public bool ColumnAdd(int intColIndex, int intColumnId, string strColumnName, string strDataType)
      {
         _dbcColumns[intColIndex] = new DBColumn(strColumnName, intColumnId, strDataType);
         //bool blnIsPrimary = false;
#if !TestDatabase
         //if (strColumnName.StartsWith("fkID"))
         //{
         //   _strForeignKeyList.Add(strColumnName);
         //   strDataType = "Int32";
         //}
         //else if (strColumnName.StartsWith("ID"))
         //{
         //   _strPrimaryKey = strColumnName;
         //   strDataType = "Int32";
         //   //blnIsPrimary = true;
         //}
#endif
         //DataColumn dc = new DataColumn(strColumnName, GetXMLType(strDataType));
         //if (blnIsPrimary)
         //{
         //   dc.AutoIncrement = true;
         //   dc.AutoIncrementStep = 1;
         //}
         //else
         //{
         //   switch (strDataType)
         //   {
         //      case "Float":
         //      case "Int32":
         //      case "Int16":
         //      case "Int8":
         //         dc.DefaultValue = 0;
         //         break;
         //      case "Bool":
         //         dc.DefaultValue = false;
         //         break;
         //      case "String":
         //      case "ListInt":
         //      case "ListFloat":
         //      case "Date": //Wird nicht in der DB verwendet, sondern nur für Hilfsfelder
         //      default:
         //         // kein Default-Wert
         //         break;
         //   }
         //}
         DataColumn dc = new DataColumn(strColumnName, GetXMLType(""));
         base.Columns.Add(dc);
         return true;
      }

      public DataType ColumnDataType(int intColIndex)
      {
         switch (_dbcColumns[intColIndex].DataType())
         {
            case "Bool":
               return DataType.Bool;
            case "Float":
               return DataType.Float;
            case "ListInt":
            case "ListFloat":
               return DataType.List;
            default:
               return DataType.Normal;
         }
      }

      #endregion

      
      #region Value

      public bool SetValue(int intRow, int intCol, string strValue)
      {
         base.Rows[intRow][intCol] = strValue;
         return true;
      }

      public bool SetValue(int intRow, int intCol, decimal dcmValue)
      {
         base.Rows[intRow][intCol] = dcmValue;
         return true;
      }

      public bool SetValue(int intRow, int intCol, bool blnValue)
      {
         base.Rows[intRow][intCol] = blnValue;
         return true;
      }

      #endregion

      #endregion


      #region PRIVATE

      private Type GetXMLType(string strDataType)
      {
         switch (strDataType)
         { 
            case "Float":
               return Type.GetType("System.Decimal");
            case "Int32":
               return Type.GetType("System.Int32");
            case "Int16":
               return Type.GetType("System.Int16");
            case "Int8":
               return Type.GetType("System.Int16"); //Byte
            case "Bool":
               return Type.GetType("System.Boolean");
            case "String":
            case "ListInt":
            case "ListFloat":
               return Type.GetType("System.String");
            case "Date": //Wird nicht in der DB verwendet, sondern nur für Hilfsfelder
               return Type.GetType("System.DateTime");
            default:
               return Type.GetType("System.String");
         }
      }

      #endregion
   }
}
