using System.Data;
using System;

namespace ExcelExporter
{
   public class DBTable : DataTable
   {

      public DBTable() { }

      public DBTable(string strTableName, int intTableId, int intNumOrigCols, int intNumCols, int intNumRows, string[] strColumns, string[,] strCells) : base(strTableName)
      {
         // Alle Spalten anlegen gemäss Definition
         foreach (string s in strColumns)
            base.Columns.Add(new DataColumn(s, Type.GetType("System.String")));

         // Daten einlesen
         for (int r = 0; r < intNumRows; ++r)
         {
            DataRow dr = this.NewRow();
            for (int c = 0; c < intNumCols; ++c)
               dr[c] = strCells[r, c];
            this.Rows.Add(dr);
         }
      }

   }
}
