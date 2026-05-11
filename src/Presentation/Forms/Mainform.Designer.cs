namespace FCT.G6T.Presentation.Forms
{
    partial class Mainform
    {
        private System.ComponentModel.IContainer components = null;

        // LEFT
        private Panel pnlCamera;
        private Button btnStart;
        private ComboBox cbDiCom;
        private ComboBox cbG6tCom;

        private Label lblSerial;
        private TextBox txtSerial;

        private GroupBox grpTest;
        private Panel led_1;
        private Panel led_2;
        private Panel led_3;
        private Panel led_4;

        private RichTextBox txtLog;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            pnlCamera = new Panel();
            btnStart = new Button();
            cbDiCom = new ComboBox();
            cbG6tCom = new ComboBox();
            lblSerial = new Label();
            txtSerial = new TextBox();
            grpTest = new GroupBox();
            panel1 = new Panel();
            labelLedTest = new Label();
            labelButtonTest = new Label();
            labelLoraTest = new Label();
            labelReadValueTest = new Label();
            ledButtonTest = new Panel();
            ledLoraTest = new Panel();
            ledReadValueTest = new Panel();
            led_1 = new Panel();
            led_2 = new Panel();
            led_3 = new Panel();
            led_4 = new Panel();
            txtLog = new RichTextBox();
            pnlStatus = new Panel();
            radioButton2 = new RadioButton();
            radioButton3 = new RadioButton();
            radioButton4 = new RadioButton();
            radioButton5 = new RadioButton();
            comboBox1 = new ComboBox();
            btnStop = new Button();
            groupBox1 = new GroupBox();
            button1 = new Button();
            button2 = new Button();
            button3 = new Button();
            btnClear = new Button();
            groupBox2 = new GroupBox();
            radioButton7 = new RadioButton();
            radioButton9 = new RadioButton();
            grpTest.SuspendLayout();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // pnlCamera
            // 
            pnlCamera.BorderStyle = BorderStyle.FixedSingle;
            pnlCamera.Location = new Point(14, 12);
            pnlCamera.Name = "pnlCamera";
            pnlCamera.Size = new Size(600, 408);
            pnlCamera.TabIndex = 0;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(189, 506);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(257, 88);
            btnStart.TabIndex = 2;
            btnStart.Text = "Start";
            btnStart.Click += btnStart_Click;
            // 
            // cbDiCom
            // 
            cbDiCom.Location = new Point(639, 52);
            cbDiCom.Name = "cbDiCom";
            cbDiCom.Size = new Size(99, 28);
            cbDiCom.TabIndex = 5;
            cbDiCom.SelectedIndexChanged += cbDiCom_SelectedIndexChanged;
            // 
            // cbG6tCom
            // 
            cbG6tCom.Location = new Point(904, 19);
            cbG6tCom.Name = "cbG6tCom";
            cbG6tCom.Size = new Size(99, 28);
            cbG6tCom.TabIndex = 7;
            cbG6tCom.SelectedIndexChanged += cbG6tCom_SelectedIndexChanged;
            // 
            // lblSerial
            // 
            lblSerial.Location = new Point(626, 114);
            lblSerial.Name = "lblSerial";
            lblSerial.Size = new Size(100, 23);
            lblSerial.TabIndex = 9;
            lblSerial.Text = "Serial:";
            lblSerial.Click += lblSerial_Click;
            // 
            // txtSerial
            // 
            txtSerial.Location = new Point(717, 111);
            txtSerial.Name = "txtSerial";
            txtSerial.Size = new Size(323, 27);
            txtSerial.TabIndex = 10;
            txtSerial.TextChanged += txtSerial_TextChanged;
            // 
            // grpTest
            // 
            grpTest.Controls.Add(panel1);
            grpTest.Controls.Add(labelLedTest);
            grpTest.Controls.Add(labelButtonTest);
            grpTest.Controls.Add(labelLoraTest);
            grpTest.Controls.Add(labelReadValueTest);
            grpTest.Controls.Add(ledButtonTest);
            grpTest.Controls.Add(ledLoraTest);
            grpTest.Controls.Add(ledReadValueTest);
            grpTest.Location = new Point(626, 233);
            grpTest.Name = "grpTest";
            grpTest.Size = new Size(600, 187);
            grpTest.TabIndex = 11;
            grpTest.TabStop = false;
            grpTest.Text = "Test Results";
            grpTest.Enter += grpTest_Enter;
            // 
            // panel1
            // 
            panel1.BackColor = Color.Gray;
            panel1.Location = new Point(250, 30);
            panel1.Name = "panel1";
            panel1.Size = new Size(15, 15);
            panel1.TabIndex = 5;
            panel1.Paint += panel1_Paint;
            // 
            // labelLedTest
            // 
            labelLedTest.AutoSize = true;
            labelLedTest.Location = new Point(20, 30);
            labelLedTest.Name = "labelLedTest";
            labelLedTest.Size = new Size(65, 20);
            labelLedTest.TabIndex = 0;
            labelLedTest.Text = "LED Test";
            // 
            // labelButtonTest
            // 
            labelButtonTest.AutoSize = true;
            labelButtonTest.Location = new Point(20, 60);
            labelButtonTest.Name = "labelButtonTest";
            labelButtonTest.Size = new Size(83, 20);
            labelButtonTest.TabIndex = 1;
            labelButtonTest.Text = "Button Test";
            // 
            // labelLoraTest
            // 
            labelLoraTest.AutoSize = true;
            labelLoraTest.Location = new Point(20, 90);
            labelLoraTest.Name = "labelLoraTest";
            labelLoraTest.Size = new Size(68, 20);
            labelLoraTest.TabIndex = 2;
            labelLoraTest.Text = "Lora Test";
            // 
            // labelReadValueTest
            // 
            labelReadValueTest.AutoSize = true;
            labelReadValueTest.Location = new Point(20, 120);
            labelReadValueTest.Name = "labelReadValueTest";
            labelReadValueTest.Size = new Size(113, 20);
            labelReadValueTest.TabIndex = 3;
            labelReadValueTest.Text = "Read Value Test";
            // 
            // ledButtonTest
            // 
            ledButtonTest.BackColor = Color.Gray;
            ledButtonTest.Location = new Point(250, 65);
            ledButtonTest.Name = "ledButtonTest";
            ledButtonTest.Size = new Size(15, 15);
            ledButtonTest.TabIndex = 4;
            // 
            // ledLoraTest
            // 
            ledLoraTest.BackColor = Color.Gray;
            ledLoraTest.Location = new Point(250, 95);
            ledLoraTest.Name = "ledLoraTest";
            ledLoraTest.Size = new Size(15, 15);
            ledLoraTest.TabIndex = 5;
            // 
            // ledReadValueTest
            // 
            ledReadValueTest.BackColor = Color.Gray;
            ledReadValueTest.Location = new Point(250, 125);
            ledReadValueTest.Name = "ledReadValueTest";
            ledReadValueTest.Size = new Size(15, 15);
            ledReadValueTest.TabIndex = 6;
            // 
            // led_1
            // 
            led_1.Location = new Point(0, 0);
            led_1.Name = "led_1";
            led_1.Size = new Size(200, 100);
            led_1.TabIndex = 0;
            // 
            // led_2
            // 
            led_2.Location = new Point(0, 0);
            led_2.Name = "led_2";
            led_2.Size = new Size(200, 100);
            led_2.TabIndex = 0;
            // 
            // led_3
            // 
            led_3.Location = new Point(0, 0);
            led_3.Name = "led_3";
            led_3.Size = new Size(200, 100);
            led_3.TabIndex = 0;
            // 
            // led_4
            // 
            led_4.Location = new Point(0, 0);
            led_4.Name = "led_4";
            led_4.Size = new Size(200, 100);
            led_4.TabIndex = 0;
            // 
            // txtLog
            // 
            txtLog.Location = new Point(626, 426);
            txtLog.Name = "txtLog";
            txtLog.Size = new Size(613, 191);
            txtLog.TabIndex = 12;
            txtLog.Text = "Log";
            txtLog.TextChanged += txtLog_TextChanged;
            // 
            // pnlStatus
            // 
            pnlStatus.BackColor = Color.Gray;
            pnlStatus.Location = new Point(14, 426);
            pnlStatus.Name = "pnlStatus";
            pnlStatus.Size = new Size(600, 68);
            pnlStatus.TabIndex = 1;
            // 
            // radioButton2
            // 
            radioButton2.AutoSize = true;
            radioButton2.Checked = true;
            radioButton2.Location = new Point(20, 36);
            radioButton2.Name = "radioButton2";
            radioButton2.Size = new Size(75, 24);
            radioButton2.TabIndex = 17;
            radioButton2.TabStop = true;
            radioButton2.Text = "Smoke";
            radioButton2.UseVisualStyleBackColor = true;
            radioButton2.CheckedChanged += radioButton2_CheckedChanged;
            // 
            // radioButton3
            // 
            radioButton3.AutoSize = true;
            radioButton3.Location = new Point(119, 36);
            radioButton3.Name = "radioButton3";
            radioButton3.Size = new Size(62, 24);
            radioButton3.TabIndex = 18;
            radioButton3.Text = "Heat";
            radioButton3.UseVisualStyleBackColor = true;
            radioButton3.CheckedChanged += radioButton3_CheckedChanged;
            // 
            // radioButton4
            // 
            radioButton4.AutoSize = true;
            radioButton4.Location = new Point(216, 36);
            radioButton4.Name = "radioButton4";
            radioButton4.Size = new Size(92, 24);
            radioButton4.TabIndex = 19;
            radioButton4.Text = "Light bell";
            radioButton4.UseVisualStyleBackColor = true;
            radioButton4.CheckedChanged += radioButton4_CheckedChanged_1;
            // 
            // radioButton5
            // 
            radioButton5.AutoSize = true;
            radioButton5.Location = new Point(340, 36);
            radioButton5.Name = "radioButton5";
            radioButton5.Size = new Size(74, 24);
            radioButton5.TabIndex = 17;
            radioButton5.Text = "Button";
            radioButton5.UseVisualStyleBackColor = true;
            radioButton5.CheckedChanged += radioButton5_CheckedChanged_1;
            // 
            // comboBox1
            // 
            comboBox1.Location = new Point(639, 18);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(100, 28);
            comboBox1.TabIndex = 20;
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(0, 0);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(75, 23);
            btnStop.TabIndex = 21;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(radioButton2);
            groupBox1.Controls.Add(radioButton5);
            groupBox1.Controls.Add(radioButton3);
            groupBox1.Controls.Add(radioButton4);
            groupBox1.Location = new Point(626, 144);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(600, 83);
            groupBox1.TabIndex = 12;
            groupBox1.TabStop = false;
            groupBox1.Text = "Choose device";
            groupBox1.Enter += groupBox1_Enter;
            // 
            // button1
            // 
            button1.Location = new Point(745, 19);
            button1.Name = "button1";
            button1.Size = new Size(122, 26);
            button1.TabIndex = 22;
            button1.Text = "QR Connect";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.Location = new Point(1009, 18);
            button2.Name = "button2";
            button2.Size = new Size(122, 28);
            button2.TabIndex = 23;
            button2.Text = "G6T Connect";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // button3
            // 
            button3.Location = new Point(744, 52);
            button3.Name = "button3";
            button3.Size = new Size(123, 29);
            button3.TabIndex = 24;
            button3.Text = "DT Connect";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // btnClear
            // 
            btnClear.Location = new Point(1145, 588);
            btnClear.Name = "btnClear";
            btnClear.Size = new Size(94, 29);
            btnClear.TabIndex = 25;
            btnClear.Text = "Clear Log";
            btnClear.UseVisualStyleBackColor = true;
            btnClear.Click += btnClear_Click;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(radioButton7);
            groupBox2.Controls.Add(radioButton9);
            groupBox2.Location = new Point(1059, 81);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(168, 57);
            groupBox2.TabIndex = 20;
            groupBox2.TabStop = false;
            groupBox2.Text = "QR";
            groupBox2.Enter += groupBox2_Enter;
            // 
            // radioButton7
            // 
            radioButton7.AutoSize = true;
            radioButton7.Checked = true;
            radioButton7.Location = new Point(6, 26);
            radioButton7.Name = "radioButton7";
            radioButton7.Size = new Size(54, 24);
            radioButton7.TabIndex = 17;
            radioButton7.TabStop = true;
            radioButton7.Text = "Use";
            radioButton7.UseVisualStyleBackColor = true;
            radioButton7.CheckedChanged += radioButton7_CheckedChanged;
            // 
            // radioButton9
            // 
            radioButton9.AutoSize = true;
            radioButton9.Location = new Point(86, 26);
            radioButton9.Name = "radioButton9";
            radioButton9.Size = new Size(76, 24);
            radioButton9.TabIndex = 18;
            radioButton9.Text = "No use";
            radioButton9.UseVisualStyleBackColor = true;
            radioButton9.CheckedChanged += radioButton7_CheckedChanged;
            // 
            // Mainform
            // 
            ClientSize = new Size(1239, 628);
            Controls.Add(groupBox2);
            Controls.Add(button3);
            Controls.Add(button2);
            Controls.Add(btnClear);
            Controls.Add(button1);
            Controls.Add(groupBox1);
            Controls.Add(comboBox1);
            Controls.Add(pnlCamera);
            Controls.Add(pnlStatus);
            Controls.Add(btnStart);
            Controls.Add(cbDiCom);
            Controls.Add(cbG6tCom);
            Controls.Add(lblSerial);
            Controls.Add(txtSerial);
            Controls.Add(grpTest);
            Controls.Add(txtLog);
            Controls.Add(btnStop);
            Name = "Mainform";
            Text = "FCT G6T";
            Load += Mainform_Load;
            grpTest.ResumeLayout(false);
            grpTest.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        // helper (Designer v?n OK)
        private void AddTestRow(string text, int index, Panel led)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Location = new Point(10, 25 + index * 25);
            lbl.AutoSize = true;

            led.BackColor = Color.Gray;
            led.Size = new Size(15, 15);
            led.Location = new Point(250, 25 + index * 25);
            led.Name = "led_" + index;

            this.grpTest.Controls.Add(lbl);
            this.grpTest.Controls.Add(led);
        }

        private Label labelLedTest;
        private Label labelButtonTest;
        private Label labelLoraTest;
        private Label labelReadValueTest;
        private Panel ledButtonTest;
        private Panel ledLoraTest;
        private Panel ledReadValueTest;
        private Panel pnlStatus;
        private RadioButton radioButton2;
        private RadioButton radioButton3;
        private RadioButton radioButton4;
        private RadioButton radioButton5;
        private ComboBox comboBox1;
        private Button btnStop;
        private GroupBox groupBox1;
        private Panel panel1;
        private Button button1;
        private Button button2;
        private Button button3;
        private Button btnClear;
        private GroupBox groupBox2;
        private RadioButton radioButton7;
        private RadioButton radioButton9;
    }
}
