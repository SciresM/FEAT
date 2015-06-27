namespace Fire_Emblem_Awakening_Archive_Tool
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.RTB_Output = new System.Windows.Forms.RichTextBox();
            this.TB_FilePath = new System.Windows.Forms.TextBox();
            this.B_Go = new System.Windows.Forms.Button();
            this.B_Open = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // RTB_Output
            // 
            this.RTB_Output.BackColor = System.Drawing.SystemColors.Control;
            this.RTB_Output.Location = new System.Drawing.Point(9, 40);
            this.RTB_Output.Name = "RTB_Output";
            this.RTB_Output.ReadOnly = true;
            this.RTB_Output.Size = new System.Drawing.Size(450, 269);
            this.RTB_Output.TabIndex = 12;
            this.RTB_Output.Text = "Open a file, or Drag/Drop several! Click this box to clear its text.\n";
            this.RTB_Output.Click += new System.EventHandler(this.RTB_Output_Click);
            // 
            // TB_FilePath
            // 
            this.TB_FilePath.Location = new System.Drawing.Point(98, 12);
            this.TB_FilePath.Name = "TB_FilePath";
            this.TB_FilePath.ReadOnly = true;
            this.TB_FilePath.Size = new System.Drawing.Size(275, 20);
            this.TB_FilePath.TabIndex = 11;
            this.TB_FilePath.TextChanged += new System.EventHandler(this.TB_FilePath_TextChanged);
            // 
            // B_Go
            // 
            this.B_Go.Enabled = false;
            this.B_Go.ForeColor = System.Drawing.SystemColors.ControlText;
            this.B_Go.Location = new System.Drawing.Point(379, 9);
            this.B_Go.Name = "B_Go";
            this.B_Go.Size = new System.Drawing.Size(80, 25);
            this.B_Go.TabIndex = 10;
            this.B_Go.Text = "Go";
            this.B_Go.UseVisualStyleBackColor = true;
            // 
            // B_Open
            // 
            this.B_Open.Location = new System.Drawing.Point(9, 9);
            this.B_Open.Name = "B_Open";
            this.B_Open.Size = new System.Drawing.Size(84, 25);
            this.B_Open.TabIndex = 9;
            this.B_Open.Text = "Open";
            this.B_Open.UseVisualStyleBackColor = true;
            this.B_Open.Click += new System.EventHandler(this.B_Open_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(469, 321);
            this.Controls.Add(this.RTB_Output);
            this.Controls.Add(this.TB_FilePath);
            this.Controls.Add(this.B_Go);
            this.Controls.Add(this.B_Open);
            this.MaximumSize = new System.Drawing.Size(485, 360);
            this.MinimumSize = new System.Drawing.Size(485, 360);
            this.Name = "Form1";
            this.Text = "Fire Emblem Archive Tool";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RichTextBox RTB_Output;
        private System.Windows.Forms.TextBox TB_FilePath;
        private System.Windows.Forms.Button B_Go;
        private System.Windows.Forms.Button B_Open;
    }
}

