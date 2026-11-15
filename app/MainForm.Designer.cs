using System.Drawing;
using System.Windows.Forms;

namespace SoundReplacer.App
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private Panel panelHeader;
        private Label lblGameDirCaption;
        private Label lblGameDir;
        private Label lblModDirCaption;
        private Label lblModDir;
        private Label lblBanner;
        private Button btnOpenModFolder;
        private Button btnBrowseGame;

        private DataGridView dgvSounds;
        private DataGridViewTextBoxColumn colSound;
        private DataGridViewTextBoxColumn colPlays;
        private DataGridViewTextBoxColumn colFile;
        private DataGridViewButtonColumn colPick;
        private DataGridViewButtonColumn colClear;

        private Panel panelFooter;
        private TextBox txtNewSound;
        private Button btnAddSound;
        private Button btnClearRow;
        private Button btnRefresh;
        private Button btnApply;
        private Button btnReset;
        private Label lblStatus;

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            this.panelHeader = new Panel();
            this.lblGameDirCaption = new Label();
            this.lblGameDir = new Label();
            this.lblModDirCaption = new Label();
            this.lblModDir = new Label();
            this.lblBanner = new Label();
            this.btnOpenModFolder = new Button();
            this.btnBrowseGame = new Button();

            this.dgvSounds = new DataGridView();
            this.colSound = new DataGridViewTextBoxColumn();
            this.colPlays = new DataGridViewTextBoxColumn();
            this.colFile = new DataGridViewTextBoxColumn();
            this.colPick = new DataGridViewButtonColumn();
            this.colClear = new DataGridViewButtonColumn();

            this.panelFooter = new Panel();
            this.txtNewSound = new TextBox();
            this.btnAddSound = new Button();
            this.btnClearRow = new Button();
            this.btnRefresh = new Button();
            this.btnApply = new Button();
            this.btnReset = new Button();
            this.lblStatus = new Label();

            this.panelHeader.SuspendLayout();
            this.panelFooter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSounds)).BeginInit();
            this.SuspendLayout();

            // ---------------- header ----------------
            this.panelHeader.Dock = DockStyle.Top;
            this.panelHeader.Height = 92;
            this.panelHeader.Controls.Add(this.lblGameDirCaption);
            this.panelHeader.Controls.Add(this.lblGameDir);
            this.panelHeader.Controls.Add(this.lblModDirCaption);
            this.panelHeader.Controls.Add(this.lblModDir);
            this.panelHeader.Controls.Add(this.lblBanner);
            this.panelHeader.Controls.Add(this.btnOpenModFolder);
            this.panelHeader.Controls.Add(this.btnBrowseGame);

            this.lblGameDirCaption.AutoSize = true;
            this.lblGameDirCaption.Location = new Point(0, 6);
            this.lblGameDirCaption.Text = "Папка игры:";

            this.lblGameDir.AutoEllipsis = true;
            this.lblGameDir.Location = new Point(96, 4);
            this.lblGameDir.Size = new Size(750, 20);
            this.lblGameDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.lblGameDir.Text = "не найдена";

            this.btnOpenModFolder.Location = new Point(790, 2);
            this.btnOpenModFolder.Size = new Size(134, 28);
            this.btnOpenModFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnOpenModFolder.Text = "Открыть папку мода";
            this.btnOpenModFolder.UseVisualStyleBackColor = true;

            this.btnBrowseGame.Location = new Point(930, 2);
            this.btnBrowseGame.Size = new Size(126, 28);
            this.btnBrowseGame.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnBrowseGame.Text = "Обзор…";
            this.btnBrowseGame.UseVisualStyleBackColor = true;

            this.lblModDirCaption.AutoSize = true;
            this.lblModDirCaption.Location = new Point(0, 34);
            this.lblModDirCaption.Text = "Папка мода:";

            this.lblModDir.AutoEllipsis = true;
            this.lblModDir.Location = new Point(96, 32);
            this.lblModDir.Size = new Size(750, 20);
            this.lblModDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.lblModDir.Text = "—";

            this.lblBanner.AutoEllipsis = true;
            this.lblBanner.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.lblBanner.ForeColor = Color.Firebrick;
            this.lblBanner.Location = new Point(0, 62);
            this.lblBanner.Size = new Size(1056, 22);
            this.lblBanner.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.lblBanner.Visible = false;
            this.lblBanner.Text = "";

            // ---------------- grid ----------------
            this.dgvSounds.Dock = DockStyle.Fill;
            this.dgvSounds.AllowUserToAddRows = false;
            this.dgvSounds.AllowUserToDeleteRows = false;
            this.dgvSounds.AllowUserToResizeRows = false;
            this.dgvSounds.RowHeadersVisible = false;
            this.dgvSounds.MultiSelect = false;
            this.dgvSounds.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dgvSounds.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvSounds.BackgroundColor = Color.White;
            this.dgvSounds.BorderStyle = BorderStyle.FixedSingle;
            this.dgvSounds.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvSounds.ColumnHeadersHeight = 32;
            this.dgvSounds.RowTemplate.Height = 30;
            this.dgvSounds.EditMode = DataGridViewEditMode.EditProgrammatically;
            this.dgvSounds.ShowCellToolTips = true;
            this.dgvSounds.Columns.AddRange(new DataGridViewColumn[]
            {
                this.colSound, this.colPlays, this.colFile, this.colPick, this.colClear
            });

            this.colSound.HeaderText = "Звук в игре";
            this.colSound.Name = "colSound";
            this.colSound.ReadOnly = true;
            this.colSound.FillWeight = 46F;
            this.colSound.MinimumWidth = 200;

            this.colPlays.HeaderText = "Проигрываний";
            this.colPlays.Name = "colPlays";
            this.colPlays.ReadOnly = true;
            this.colPlays.FillWeight = 10F;
            this.colPlays.MinimumWidth = 90;
            this.colPlays.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            this.colFile.HeaderText = "Кастомный файл";
            this.colFile.Name = "colFile";
            this.colFile.ReadOnly = true;
            this.colFile.FillWeight = 34F;
            this.colFile.MinimumWidth = 180;

            this.colPick.HeaderText = "";
            this.colPick.Name = "colPick";
            this.colPick.Text = "Выбрать…";
            this.colPick.UseColumnTextForButtonValue = true;
            this.colPick.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            this.colPick.Width = 110;
            this.colPick.FlatStyle = FlatStyle.Flat;

            this.colClear.HeaderText = "";
            this.colClear.Name = "colClear";
            this.colClear.Text = "Очистить";
            this.colClear.UseColumnTextForButtonValue = true;
            this.colClear.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            this.colClear.Width = 94;
            this.colClear.FlatStyle = FlatStyle.Flat;

            // ---------------- footer ----------------
            this.panelFooter.Dock = DockStyle.Bottom;
            this.panelFooter.Height = 78;
            this.panelFooter.Controls.Add(this.txtNewSound);
            this.panelFooter.Controls.Add(this.btnAddSound);
            this.panelFooter.Controls.Add(this.btnClearRow);
            this.panelFooter.Controls.Add(this.btnRefresh);
            this.panelFooter.Controls.Add(this.btnApply);
            this.panelFooter.Controls.Add(this.btnReset);
            this.panelFooter.Controls.Add(this.lblStatus);

            this.txtNewSound.Location = new Point(0, 12);
            this.txtNewSound.Size = new Size(250, 26);
            this.txtNewSound.PlaceholderText = "имя звука вручную";

            this.btnAddSound.Location = new Point(258, 10);
            this.btnAddSound.Size = new Size(116, 30);
            this.btnAddSound.Text = "Добавить звук";
            this.btnAddSound.UseVisualStyleBackColor = true;

            this.btnClearRow.Location = new Point(380, 10);
            this.btnClearRow.Size = new Size(124, 30);
            this.btnClearRow.Text = "Очистить строку";
            this.btnClearRow.UseVisualStyleBackColor = true;

            this.btnRefresh.Location = new Point(510, 10);
            this.btnRefresh.Size = new Size(150, 30);
            this.btnRefresh.Text = "Обновить каталог";
            this.btnRefresh.UseVisualStyleBackColor = true;

            this.btnApply.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnApply.Location = new Point(786, 10);
            this.btnApply.Size = new Size(126, 30);
            this.btnApply.Text = "Применить";
            this.btnApply.UseVisualStyleBackColor = true;

            this.btnReset.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnReset.Location = new Point(918, 10);
            this.btnReset.Size = new Size(138, 30);
            this.btnReset.Text = "Сбросить";
            this.btnReset.UseVisualStyleBackColor = true;

            this.lblStatus.AutoEllipsis = true;
            this.lblStatus.ForeColor = Color.DimGray;
            this.lblStatus.Location = new Point(0, 50);
            this.lblStatus.Size = new Size(1056, 20);
            this.lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.lblStatus.Text = "";

            // ---------------- form ----------------
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1080, 720);
            this.MinimumSize = new Size(940, 620);
            this.Controls.Add(this.dgvSounds);
            this.Controls.Add(this.panelFooter);
            this.Controls.Add(this.panelHeader);
            this.Font = new Font("Segoe UI", 9F);
            this.Padding = new Padding(12);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "SoundReplacer — замена звуков R.E.P.O.";
            this.Load += new System.EventHandler(this.MainForm_Load);

            this.dgvSounds.CellContentClick += new DataGridViewCellEventHandler(this.dgvSounds_CellContentClick);
            this.dgvSounds.CellDoubleClick += new DataGridViewCellEventHandler(this.dgvSounds_CellDoubleClick);
            this.btnBrowseGame.Click += new System.EventHandler(this.btnBrowseGame_Click);
            this.btnOpenModFolder.Click += new System.EventHandler(this.btnOpenModFolder_Click);
            this.btnAddSound.Click += new System.EventHandler(this.btnAddSound_Click);
            this.btnClearRow.Click += new System.EventHandler(this.btnClearRow_Click);
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            this.FormClosing += new FormClosingEventHandler(this.MainForm_FormClosing);

            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.panelFooter.ResumeLayout(false);
            this.panelFooter.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSounds)).EndInit();
            this.ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }
    }
}