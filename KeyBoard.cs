using MetroFramework.Forms;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace STI_CoolingConveyorUpdated
{
    public partial class KeyBoard : MetroForm
    {
        private TextBox targetTextBox;
        private TextBox textBox;
        private int type;
        private bool password;
        private bool init = false;

        public KeyBoard(TextBox targetTextBox, int type, bool password = false)
        {
            this.targetTextBox = targetTextBox;
            this.type = type;
            this.password = password;
            InitializeKeyboard();

        }

        private void InitializeKeyboard()
        {
            TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
            if (type == 1)
            {
                var keys = new[]
                {
                "1", "2", "3", "4", "5", "6", "7", "8", "9", "0","<---",
                "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P","Enter",
                "A", "S", "D", "F", "G", "H", "J", "K", "L","",
                "Z", "X", "C", "V", "B", "N", "M",".","Clear"
                };

                tableLayoutPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 12,
                    RowCount = 3,
                    AutoSize = true,
                    Padding = new Padding(10)
                };

                textBox = new TextBox
                {
                    Font = new Font("Microsoft Sans Serif", 18),
                    Width = 644,
                    Height = 50,
                    CharacterCasing = CharacterCasing.Upper
                };
                if (password)
                {
                    textBox.PasswordChar = '*';
                }
                tableLayoutPanel.SetColumnSpan(textBox, 12);
                tableLayoutPanel.Controls.Add(textBox);
                textBox.Text = targetTextBox.Text;

                foreach (var key in keys)
                {
                    int width = 0;
                    int height = 0;

                    if (key == "<---" || key == "Clear")
                    {
                        width = 104;
                        height = 50;
                    }
                    else if (key == "Enter")
                    {
                        width = 104;
                        height = 104;
                    }
                    else
                    {
                        width = 50;
                        height = 50;
                    }

                    var button = new MetroFramework.Controls.MetroButton
                    {
                        Text = key,
                        Width = width,
                        Height = height,
                        Margin = new Padding(2),
                        //FontSize = MetroFramework.MetroTextBoxSize.Tall
                    };

                    button.Click += KeyButton_Click;

                    if (key == "<---" || key == "Clear")
                    {
                        tableLayoutPanel.SetColumnSpan(button, 2);
                    }
                    else if (key == "Enter")
                    {
                        tableLayoutPanel.SetRowSpan(button, 2);
                        tableLayoutPanel.SetColumnSpan(button, 2);
                    }

                    tableLayoutPanel.Controls.Add(button);
                }
            }
            else
            {
                var keys = new[]
                {
                "1", "2", "3", "<---",
                "4", "5", "6", "Enter",
                "7", "8", "9",
                "0", ".", "","Clear"
                };

                tableLayoutPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 5,
                    RowCount = 4,
                    AutoSize = true,
                    Padding = new Padding(10)
                };

                textBox = new TextBox
                {
                    Font = new Font("Microsoft Sans Serif", 18),
                    Width = 266,
                    Height = 50,
                    CharacterCasing = CharacterCasing.Upper
                };

                tableLayoutPanel.SetColumnSpan(textBox, 5);
                tableLayoutPanel.Controls.Add(textBox);
                textBox.Text = targetTextBox.Text;

                foreach (var key in keys)
                {
                    int width = 0;
                    int height = 0;

                    if (key == "<---" || key == "Clear")
                    {
                        width = 104;
                        height = 50;
                    }
                    else if (key == "Enter")
                    {
                        width = 104;
                        height = 104;
                    }
                    else
                    {
                        width = 50;
                        height = 50;
                    }

                    var button = new MetroFramework.Controls.MetroButton
                    {
                        Text = key,
                        Width = width,
                        Height = height,
                        Margin = new Padding(2),
                        //FontSize = MetroFramework.MetroTextBoxSize.Tall
                    };

                    button.Click += KeyButton_Click;

                    if (key == "<---" || key == "Clear")
                    {
                        tableLayoutPanel.SetColumnSpan(button, 2);
                    }
                    else if (key == "Enter")
                    {
                        tableLayoutPanel.SetRowSpan(button, 2);
                        tableLayoutPanel.SetColumnSpan(button, 2);
                    }

                    tableLayoutPanel.Controls.Add(button);
                }
            }

            textBox.KeyPress += new KeyPressEventHandler(KeyPress_TextBox);
            Controls.Add(tableLayoutPanel);
            Text = "KEYBORAD";
            AutoSize = true;
            StartPosition = FormStartPosition.CenterParent;
        }

        private void KeyPress_TextBox(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                // Close the keyboard and indicate completion
                this.DialogResult = DialogResult.OK;
                targetTextBox.Text = textBox.Text;
                Close();
            }
        }

        private void KeyButton_Click(object sender, EventArgs e)
        {
            if (sender is MetroFramework.Controls.MetroButton button)
            {

                var key = button.Text;
                if (!init && key != "Enter")
                {
                    textBox.Clear();
                    init = true;
                }
                if (key == "Space")
                {
                    textBox.AppendText(" ");
                }
                else if (key == "<---")
                {
                    if (textBox.Text.Length > 0)
                    {
                        textBox.Text = textBox.Text.Substring(0, textBox.Text.Length - 1);
                        textBox.SelectionStart = textBox.Text.Length;
                    }
                }
                else if (key == "Enter")
                {
                    // Close the keyboard and indicate completion
                    this.DialogResult = DialogResult.OK;
                    targetTextBox.Text = textBox.Text;
                    Close();
                }
                else if (key == "Clear")
                {
                    textBox.Clear();
                }
                else
                {
                    textBox.AppendText(key);
                }
                if (key != "Enter")
                {
                    textBox.Focus();
                }
            }
        }

        private void KeyBoard_Load(object sender, EventArgs e)
        {

        }
    }
}
