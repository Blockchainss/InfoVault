using Npgsql;
using System;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using System.Configuration;  

namespace Info
{
    public partial class MainForm : Form
    {
        private NpgsqlConnection conn;
        private DataTable partners;
        private DataTable partner_types;
        private DataTable partner_products;
        private DataTable realisation_history;
        private DataTable material_types;
        private DataTable products;
        private DataTable product_types;
        private int? selected_id;

        public MainForm()
        {
            InitializeComponent();
            string connectionString = "Server=localhost;Port=5433;Database=company_db;User Id=postgres;Password=1234;";
            conn = new NpgsqlConnection(connectionString);
            try
            {
                conn.Open();
                UpdateDataTables();
            }
            catch (NpgsqlException ex)
            {
                ShowErrorMessage($"Ошибка подключения к базе данных: {ex.Message}");
            }
            
        }
        private void MainForm_FormClosed(object sender, FormClosedEventArgs e) => conn.Close();

        private void UpdateDataTables()
        {
            partners = ExecuteQuery("SELECT * FROM partners p JOIN partner_types t ON p.partner_type = t.id;");
            partner_products = ExecuteQuery("SELECT p.id, SUM(pp.amount) AS total_amount FROM partners p JOIN partner_products pp ON pp.partner = p.id GROUP BY p.id;");
            partner_types = ExecuteQuery("SELECT * FROM partner_types;");
            realisation_history = ExecuteQuery("SELECT p.id, p.name, pr.name AS pr_name, pp.article, pp.amount, pp.date FROM partner_products pp JOIN products pr ON pp.article = pr.article JOIN partners p ON pp.partner = p.id;");
            material_types = ExecuteQuery("SELECT * FROM material_types;");
            products = ExecuteQuery("SELECT * FROM products;");
            product_types = ExecuteQuery("SELECT * FROM product_types;");

            PopulateDataGrids();
        }

        private void PopulateDataGrids()
        {
            PopulateDataGridView1();
            PopulateDataGridView2();
            PopulateDataGridView3();
            dataGridView4.DataSource = product_types;
            dataGridView5.DataSource = material_types;
        }

        private void PopulateDataGridView1()
        {
            dataGridView1.Rows.Clear();
            foreach (DataRow row in partners.Rows)
            {
                int discount = CalculateDiscount(row);
                string formattedText = FormatPartnerRow(row);
                dataGridView1.Rows.Add(formattedText, $"{discount}%");
            }
        }

        private int CalculateDiscount(DataRow row)
        {
            var totalAmountRow = partner_products.AsEnumerable().FirstOrDefault(r => r["id"].ToString() == row["id"].ToString());
            int discountAmount = totalAmountRow == null ? 0 : Convert.ToInt32(totalAmountRow["total_amount"]);

            return discountAmount >= 300000 ? 15 :
                   discountAmount >= 50000 ? 10 :
                   discountAmount >= 10000 ? 5 : 0;
        }

        private string FormatPartnerRow(DataRow row)
        {
            return $"{row["type"]} | {row["name"]}\n" +
                   $"Директор: {row["director"]}\n" +
                   $"Телефон: {row["phone"]}\n" +
                   $"Рейтинг: {row["rating"]}";
        }

        private void PopulateDataGridView2()
        {
            dataGridView2.Rows.Clear();
            foreach (DataRow row in partners.Rows)
            {
                dataGridView2.Rows.Add(row.ItemArray);
            }
        }

        private void PopulateDataGridView3()
        {
            dataGridView3.Rows.Clear();
            foreach (DataRow row in realisation_history.Rows)
            {
                dataGridView3.Rows.Add(row["id"], row["name"], row["pr_name"], row["article"], row["amount"], Convert.ToDateTime(row["date"]).ToString("yyyy-MM-dd"));
            }
        }

        private DataTable ExecuteQuery(string command_str)
        {
            try
            {
                using (var command = new NpgsqlCommand(command_str, conn))
                {
                    if (command_str.StartsWith("INSERT") || command_str.StartsWith("DELETE") || command_str.StartsWith("UPDATE"))
                    {
                        command.ExecuteNonQuery();
                        return new DataTable(); 
                    }
                    else
                    {
                        using (var dataReader = command.ExecuteReader())
                        {
                            DataTable data = new DataTable();
                            data.Load(dataReader);
                            return data;
                        }
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                ShowErrorMessage($"Ошибка выполнения запроса к БД: {ex.Message}");
                return new DataTable(); 
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Произошла ошибка: {ex.Message}");
                return new DataTable(); 
            }
        }


        private void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void button_add_Click(object sender, EventArgs e)
        {
            TryHandlePartnerData(((Button)sender).Name);
        }

        private void TryHandlePartnerData(string senderName)
        {
            try
            {
                string[] formData = GetPartnerFormData(senderName);

                if (IsValidPartnerData(formData))
                {
                    HandlePartnerData(senderName, formData);
                    UpdateDataTables();
                }
                else
                {
                    throw new Exception();
                }
            }
            catch
            {
                ShowErrorMessage("Ошибка: данные неверны. Проверьте правильность ввода.");
            }
        }

        private string[] GetPartnerFormData(string sender)
        {
            return sender == "button_add" ?
                new string[] {
                    textBox_type_add.Text,
                    textBox_name_add.Text,
                    textBox_inn_add.Text,
                    textBox_email_add.Text,
                    textBox_address_add.Text,
                    textBox_rating_add.Text,
                    textBox_phone_add.Text,
                    textBox_director_add.Text
                } :
                new string[] {
                    textBox_type_edit.Text,
                    textBox_name_edit.Text,
                    textBox_inn_edit.Text,
                    textBox_email_edit.Text,
                    textBox_address_edit.Text,
                    textBox_rating_edit.Text,
                    textBox_phone_edit.Text,
                    textBox_director_edit.Text
                };
        }

        private bool IsValidPartnerData(string[] data)
        {
            return data.All(value => !string.IsNullOrWhiteSpace(value) && !value.Contains("'") && !value.Contains('"')) &&
                   int.TryParse(data[5], out int rating) && rating > 0;
        }

        private void HandlePartnerData(string sender, string[] data)
        {
            int partner_type = GetPartnerTypeId(data[0]);

            if (sender == "button_add")
            {
                ExecuteQuery($"INSERT INTO partners (partner_type, name, email, phone, address, inn, rating, director) VALUES ({partner_type}, '{data[1]}', '{data[3]}', '{data[6]}', '{data[4]}', '{data[2]}', {data[5]}, '{data[7]}')");
            }
            else
            {
                if (dataGridView2.SelectedRows.Count > 0)
                {
                    ExecuteQuery($"UPDATE partners SET partner_type = {partner_type}, name = '{data[1]}', email = '{data[3]}', phone = '{data[6]}', address = '{data[4]}', inn = '{data[2]}', rating = {data[5]}, director = '{data[7]}' WHERE id = {selected_id}");
                }
                else
                {
                    throw new Exception();
                }
            }
        }

        private int GetPartnerTypeId(string type)
        {
            if (!partner_types.AsEnumerable().Any(row => row["type"]?.ToString() == type))
            {
                ExecuteQuery($"INSERT INTO partner_types (type) VALUES ('{type}');");
                partner_types = ExecuteQuery("SELECT * FROM partner_types;");
            }

            return Convert.ToInt32(partner_types.AsEnumerable().FirstOrDefault(row => row["type"]?.ToString() == type)["id"]);
        }

        private void button_del_Click(object sender, EventArgs e)
        {
            if (selected_id != null && ConfirmDeletion())
            {
                ExecuteQuery($"DELETE FROM partners WHERE id = {selected_id};");
                UpdateDataTables();
            }
        }

        private bool ConfirmDeletion()
        {
            return (MessageBox.Show("Вы уверены, что хотите удалить выбранного партнера?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
        }

        private void dataGridView2_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                if (dataGridView2.SelectedRows.Count > 0)
                {
                    var row = dataGridView2.SelectedRows[0].Cells;
                    selected_id = Convert.ToInt32(row["id"].Value);
                    LoadPartnerDataToEdit(row);
                }
                else
                {
                    selected_id = null;
                }
            }
            catch
            {
                ShowErrorMessage("Ошибка при выборе строки.");
            }
        }

        private void LoadPartnerDataToEdit(DataGridViewCellCollection row)
        {
            textBox_type_edit.Text = row["type"].Value.ToString();
            textBox_name_edit.Text = row["name"].Value.ToString();
            textBox_inn_edit.Text = row["inn"].Value.ToString();
            textBox_email_edit.Text = row["email"].Value.ToString();
            textBox_address_edit.Text = row["address"].Value.ToString();
            textBox_rating_edit.Text = row["rating"].Value.ToString();
            textBox_phone_edit.Text = row["phone"].Value.ToString();
            textBox_director_edit.Text = row["director"].Value.ToString();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e) => UpdateDataTables();

        private int CalculateMaterial(int product_type, int material_type, int amount)
        {
            try
            {
                DataRow productRow = product_types.AsEnumerable().FirstOrDefault(r => Convert.ToInt32(r["id"]) == product_type);
                DataRow materialRow = material_types.AsEnumerable().FirstOrDefault(r => Convert.ToInt32(r["id"]) == material_type);

                if (productRow != null && materialRow != null && amount > 0)
                {
                    double defect_percentage = Convert.ToDouble(materialRow["percentage"]);
                    double rate = Convert.ToDouble(productRow["rate"]);
                    double material = rate * amount;
                    return Convert.ToInt32(material * (1 + defect_percentage));
                }
                else
                {
                    ShowErrorMessage("Некорректные значения параметров.");
                    return -1;
                }
            }
            catch
            {
                ShowErrorMessage("Некорректные значения параметров.");
                return -1;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                int result = CalculateMaterial(Convert.ToInt32(textBox1.Text), Convert.ToInt32(textBox2.Text), Convert.ToInt32(textBox3.Text));
                if (result >= 0)
                    textBox4.Text = result.ToString();
            }
            catch
            {
                ShowErrorMessage("Некорректные значения параметров.");
            }
        }

        private void MainForm_Load(object sender, EventArgs e) { }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }
    }
}
