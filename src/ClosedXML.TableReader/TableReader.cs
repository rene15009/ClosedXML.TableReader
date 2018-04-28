﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.TableReader.Attributes;
using ClosedXML.TableReader.Model;

// ReSharper disable once CheckNamespace
namespace ClosedXML.Excel
{

    public static class TableReader
    {
        /// <summary>
        /// Read All Data from Excel insto DataSet. Will be create a DataTable by WorkbookSheet
        /// </summary>
        /// <param name="wb">ClosedXml WorkBook instance</param>
        /// <param name="options">Read options <see cref="ReadOptions"/></param>
        /// <returns></returns>
        public static DataSet ReadTables(this IXLWorkbook wb, ReadOptions options = null)
        {
            DataSet ds = new DataSet();

            foreach (var workBookWorksheet in wb.Worksheets)
            {
                var dt = ReadExcelSheet(workBookWorksheet, options);

                ds.Tables.Add(dt);
            }

            return ds;

        }

        /// <summary>
        /// Read Table from Excel Sheet into a memory DataTable
        /// </summary>
        /// <param name="wb">ClosedXml WorkBook instance</param>
        /// <param name="sheetNumber">Workbook sheet number to read</param>
        /// <param name="options">Read options <see cref="ReadOptions"/></param>
        /// <returns></returns>
        public static DataTable ReadTable(this IXLWorkbook wb, int sheetNumber, ReadOptions options = null)
        {
            if (sheetNumber <= 0 || sheetNumber > wb.Worksheets.Count)
            {
                throw new IndexOutOfRangeException($"{nameof(sheetNumber)} is Out of Range");
            }

            var ws = wb.Worksheet(sheetNumber);
            return ReadExcelSheet(ws, options);
        }

        /// <summary>
        /// Read Table from Excel Sheet into a typed IEnumerable Collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="wb">ClosedXml WorkBook instance</param>
        /// <param name="sheetNumber">Workbook sheet number to read</param>
        /// <param name="options">Read options <see cref="ReadOptions"/></param>
        /// <returns></returns>
        public static IEnumerable<T> ReadTable<T>(this IXLWorkbook wb, int sheetNumber, ReadOptions options = null) where T : class, new()
        {
            if (sheetNumber <= 0 || sheetNumber > wb.Worksheets.Count)
            {
                throw new IndexOutOfRangeException($"{nameof(sheetNumber)} is Out of Range");
            }


            var ws = wb.Worksheet(sheetNumber);
            var dt = ReadExcelSheet(ws, options);

            return dt.AsEnumerableTyped<T>(options);

        }


        #region Private Functions



        private static DataTable ReadExcelSheet(IXLWorksheet workSheet, ReadOptions options)
        {
            DataTable dt = new DataTable();

            options = options ?? ReadOptions.DefaultOptions;


            //primera fila de titulos
            bool firstRow = options.TitlesInFirstRow;
            dt.TableName = workSheet.GetNameForDataTable();

            if (options.TitlesInFirstRow)
            {
                //si no tenemos títulos en la tabla utilizamos los nombres de columna del excel para la definición del DataTable
                foreach (var col in workSheet.ColumnsUsed())
                {
                    dt.Columns.Add(col.ColumnLetter());
                }
            }


            foreach (IXLRow row in workSheet.RowsUsed())
            {
                //Usamos la primera fila para crear las columnas con los títulos
                //init with options.TitlesInFirstRow 
                if (firstRow)
                {
                    foreach (IXLCell cell in row.CellsUsed())
                    {
                        dt.Columns.Add(cell.GetContentWithOutSpaces());
                    }
                    firstRow = false;
                }
                else
                {
                    dt.Rows.Add();
                    int i = 0;

                    foreach (IXLCell cell in row.Cells(row.FirstCellUsed().Address.ColumnNumber, row.LastCellUsed().Address.ColumnNumber))
                    {
                        dt.Rows[dt.Rows.Count - 1][i] = cell.Value.ToString();
                        i++;
                    }
                }
            }

            return dt;
        }


        /// <summary>
        /// Converts a DataTable to a list with generic objects
        /// </summary>
        /// <typeparam name="T">Generic object</typeparam>
        /// <param name="table">DataTable</param>
        /// <param name="options"></param>
        /// <returns>List with generic objects</returns>
        private static IEnumerable<T> AsEnumerableTyped<T>(this DataTable table, ReadOptions options) where T : class, new()
        {

            foreach (DataRow row in table.Rows)
            {
                T obj = new T();

                foreach (var prop in obj.GetType().GetProperties())
                {
                    try
                    {
                        PropertyInfo p = obj.GetType().GetProperty(prop.Name);

                        if (p == null) continue;


                        

                        string GetFieldNameFromCustomAttribute()
                        {
                            var atts = p.CustomAttributes.FirstOrDefault(c => c.AttributeType == typeof(ColumnTittleAttribute)
                                                                          || c.AttributeType == typeof(ColumnNameAttribute));
                            if (options==null || options.TitlesInFirstRow)
                            {
                                return atts != null && !string.IsNullOrEmpty(p.GetCustomAttribute<ColumnTittleAttribute>().Title)
                                       ? p.GetCustomAttribute<ColumnTittleAttribute>().Title.Replace(" ", string.Empty)
                                       : p.Name;
                            }
                            else
                            {
                                return atts != null && !string.IsNullOrEmpty(p.GetCustomAttribute<ColumnNameAttribute>().ColumnName)
                                    ? p.GetCustomAttribute<ColumnNameAttribute>().ColumnName
                                    : p.Name;
                            }
                        }


                        var fieldName = GetFieldNameFromCustomAttribute();


                        //Converters to parse data to typed field
                        if (options?.Converters != null && options.Converters.Any() && options.Converters.ContainsKey(p.Name))
                        {

                            var objValue = row[fieldName];
                            var exp = options.Converters[p.Name];
                            var f = exp.Compile();
                            p.SetValue(obj, Convert.ChangeType(f.DynamicInvoke(objValue), p.PropertyType), null);
                        }
                        else
                        {
                            p.SetValue(obj,
                                Convert.ChangeType(row[fieldName], p.PropertyType), null);
                        }


                    }
                    catch (Exception ex)
                    {
                        PropertyInfo p = obj.GetType().GetProperty(prop.Name);

                        Console.WriteLine($"{ex.Message} {p.Name}");
                        continue;
                    }
                }

                yield return obj;

                // list.Add(obj);
            }

        }
    }

    #endregion


}
