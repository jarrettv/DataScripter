using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Data;
using Microsoft.SqlServer.Management.UI.VSIntegration;
using System.Text.RegularExpressions;

namespace iucon.ssms.DataScripter
{
    public class MenuItem : ToolsMenuItemBase, IWinformsMenuHandler
    {
        public MenuItem()
        {
            this.Text = "Script Data as INSERT";
        }

        protected override void Invoke()
        {
            
        }
        
        public override object Clone()
        {
            return new MenuItem();
        }

        #region IWinformsMenuHandler Members

        public System.Windows.Forms.ToolStripItem[] GetMenuItems()
        {
            ToolStripMenuItem item = new ToolStripMenuItem("Script Data as");
            
            ToolStripMenuItem insertItem = new ToolStripMenuItem("INSERT");
            insertItem.Tag = false;
            insertItem.Click += new EventHandler(InsertItem_Click);

            ToolStripMenuItem insertItem2 = new ToolStripMenuItem("INSERT with column names");
            insertItem2.Tag = true;
            insertItem2.Click += new EventHandler(InsertItem_Click);

            item.DropDownItems.Add(insertItem);
            item.DropDownItems.Add(insertItem2);

            item.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem aboutItem = new ToolStripMenuItem("About");
            aboutItem.Image = iucon.ssms.DataScripter.Properties.Resources.information;
            aboutItem.Click += new EventHandler(AboutItem_Click);
            item.DropDownItems.Add(aboutItem);

            return new ToolStripItem[] { item };
        }

        void AboutItem_Click(object sender, EventArgs e)
        {
            new AboutDlg().ShowDialog();
        }

        #endregion

        void InsertItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            bool generateColumnNames = (bool)item.Tag;

            Regex tableRegex = new Regex(@"^Server\[[^\]]*\]/Database\[@Name='(?<Database>[^']*)'\]/Table\[@Name='(?<Table>[^']*)' and @Schema='(?<Schema>[^']*)'\]$");
            Match match = tableRegex.Match(this.Parent.Context);
            if (match != null)
            {
                string tableName = match.Groups["Table"].Value;
                string schema = match.Groups["Schema"].Value;
                string database = match.Groups["Database"].Value;
                string connectionString = this.Parent.Connection.ConnectionString + ";Database=" + database;

                SqlCommand command = new SqlCommand(string.Format("SELECT * FROM [{0}].[{1}]", schema, tableName));
                command.Connection = new SqlConnection(connectionString);
                command.Connection.Open();

                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable table = new DataTable();
                adapter.Fill(table);

                command.Connection.Close();

                StringBuilder buffer = new StringBuilder();
                
                // generate INSERT prefix
                StringBuilder prefix = new StringBuilder();
                if (generateColumnNames)
                {
                    prefix.AppendFormat("INSERT INTO [{0}].[{1}] (", schema, tableName);
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        if (i > 0) prefix.Append(", ");
                        prefix.AppendFormat("[{0}]", table.Columns[i].ColumnName);
                    }
                    prefix.Append(") VALUES (");
                }
                else
                {
                    prefix.AppendFormat("INSERT INTO [{0}].[{1}] VALUES (", schema, tableName);
                }

                // generate INSERT statements
                foreach (DataRow row in table.Rows)
                {
                    StringBuilder values = new StringBuilder();
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        if (i > 0) values.Append(", ");

                        Type dataType = table.Columns[i].DataType;

                        if (row.IsNull(i))
                        {
                            values.Append("NULL");
                        }
                        else if (dataType == typeof(int) ||
                            dataType == typeof(decimal) ||
                            dataType == typeof(long) ||
                            dataType == typeof(double) ||
                            dataType == typeof(float) ||
                            dataType == typeof(byte))
                        {
                            values.Append(row[i].ToString());
                        }
                        else if (dataType == typeof(byte[]))
                        {
                            values.Append("0x");
                            foreach (byte b in (byte[])row[i])
                            {
                                values.Append(b.ToString("x2"));
                            }
                        }
                        else
                        {
                            values.AppendFormat("'{0}'", row[i].ToString().Replace("'", "''"));
                        }
                    }
                    values.AppendFormat(");");

                    buffer.AppendLine(prefix.ToString() + values.ToString());
                }

                // create new document
                ServiceCache.ScriptFactory.CreateNewBlankScript(Microsoft.SqlServer.Management.UI.VSIntegration.Editors.ScriptType.Sql);

                // insert SQL definition to document
                EnvDTE.TextDocument doc = (EnvDTE.TextDocument)ServiceCache.ExtensibilityModel.Application.ActiveDocument.Object(null);

                doc.EndPoint.CreateEditPoint().Insert(buffer.ToString());
            }
        }
    }
}
