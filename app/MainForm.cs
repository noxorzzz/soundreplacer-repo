using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SoundReplacer.App.Services;
using SoundReplacer.Shared;

namespace SoundReplacer.App
{
    /// <summary>
    /// Единственное окно приложения.
    ///
    /// Приложение ничего не знает о внутренностях мода и никогда не вызывает его код.
    /// Всё взаимодействие — файлы в BepInEx/plugins/SoundReplacer:
    ///   catalog.json  — читаем список звуков, собранный модом во время игры;
    ///   Sounds/       — складываем выбранные пользователем файлы;
    ///   config.json   — пишем маппинг «имя звука -> путь к файлу».
    /// </summary>
    public partial class MainForm : Form
    {
        private sealed class RowState
        {
            public string SoundName;
            public int Plays;
            public string SourcePath;      // выбранный пользователем файл, ещё не скопирован
            public string RelativeTarget;  // путь, который уже лежит в config.json

            public string DisplayFile
            {
                get
                {
                    if (!string.IsNullOrEmpty(SourcePath)) return Path.GetFileName(SourcePath);
                    if (!string.IsNullOrEmpty(RelativeTarget))
                        return Path.GetFileName(RelativeTarget.Replace('/', Path.DirectorySeparatorChar));
                    return "— оригинал —";
                }
            }
        }

        private readonly AppSettings _settings;
        private readonly Dictionary<string, RowState> _rows = new Dictionary<string, RowState>(StringComparer.Ordinal);
        private readonly System.Windows.Forms.Timer _processTimer;

        private ModPaths _paths;
        private string _runningGame;

        public MainForm()
        {
            InitializeComponent();

            string settingsError;
            _settings = AppSettings.Load(out settingsError);

            _processTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _processTimer.Tick += ProcessTimer_Tick;
        }

        // ------------------------------------------------------------- lifecycle

        private void MainForm_Load(object sender, EventArgs e)
        {
            ResolveGameDirectory(true);
            _processTimer.Start();
            UpdateGameBanner();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _processTimer.Stop();
            _processTimer.Dispose();
        }

        private void ProcessTimer_Tick(object sender, EventArgs e)
        {
            UpdateGameBanner();
        }

        // ------------------------------------------------------------- game dir

        private void ResolveGameDirectory(bool allowPrompt)
        {
            string candidate = _settings.GameDirectory;

            if (!GameLocator.IsGameDirectory(candidate))
                candidate = GameLocator.AutoDetect(_settings.ProcessNames);

            if (GameLocator.IsGameDirectory(candidate))
            {
                SetGameDirectory(candidate, true);
                return;
            }

            UpdatePathLabels();
            SetStatus("Игра не найдена автоматически. Нажмите «Обзор…» и укажите папку, где лежит REPO.exe.");

            if (!allowPrompt) return;

            DialogResult answer = MessageBox.Show(
                this,
                "Не удалось найти R.E.P.O. автоматически.\n\n" +
                "Укажите папку игры вручную? (в ней должны лежать REPO.exe и папка BepInEx)",
                "SoundReplacer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes) return;

            string chosen = BrowseForGameDirectory();
            if (chosen != null) SetGameDirectory(chosen, true);
        }

        private void SetGameDirectory(string directory, bool saveSettings)
        {
            _paths = new ModPaths(directory);
            _settings.GameDirectory = _paths.GameDirectory;

            if (saveSettings)
            {
                string saveError;
                if (!_settings.Save(out saveError))
                    SetStatus("Не удалось сохранить настройки: " + saveError);
            }

            UpdatePathLabels();
            ReloadFromDisk();
        }

        private string BrowseForGameDirectory()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку R.E.P.O. — ту, где лежат REPO.exe и папка BepInEx";
                dialog.UseDescriptionForTitle = true;
                dialog.ShowNewFolderButton = false;

                if (!string.IsNullOrEmpty(_settings.GameDirectory) && Directory.Exists(_settings.GameDirectory))
                    dialog.SelectedPath = _settings.GameDirectory;

                if (dialog.ShowDialog(this) != DialogResult.OK) return null;

                if (!GameLocator.IsGameDirectory(dialog.SelectedPath))
                {
                    DialogResult answer = MessageBox.Show(
                        this,
                        "В выбранной папке не видно REPO.exe или BepInEx.\n\nВсё равно использовать её?",
                        "SoundReplacer",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (answer != DialogResult.Yes) return null;
                }

                return dialog.SelectedPath;
            }
        }

        private void UpdatePathLabels()
        {
            if (_paths == null)
            {
                lblGameDir.Text = "не найдена";
                lblModDir.Text = "—";
                lblModDir.ForeColor = Color.DimGray;
                return;
            }

            lblGameDir.Text = _paths.GameDirectory;
            lblModDir.Text = _paths.PluginDirectory;

            if (!_paths.BepInExInstalled)
            {
                lblModDir.ForeColor = Color.Firebrick;
                lblModDir.Text += "   (BepInEx не установлен)";
            }
            else if (!_paths.ModInstalled)
            {
                lblModDir.ForeColor = Color.DarkOrange;
                lblModDir.Text += "   (SoundReplacer.dll не найден)";
            }
            else
            {
                lblModDir.ForeColor = Color.Black;
            }
        }

        // ------------------------------------------------------------- data load

        private void ReloadFromDisk()
        {
            _rows.Clear();

            if (_paths == null)
            {
                RebuildGrid();
                return;
            }

            string configError;
            ReplacerConfig config = ConfigStore.Load(_paths.ConfigPath, out configError);

            SoundCatalog catalog = null;
            if (File.Exists(_paths.CatalogPath))
            {
                string catalogError;
                SoundCatalog loaded;
                if (SoundCatalog.TryLoad(_paths.CatalogPath, out loaded, out catalogError))
                    catalog = loaded;
            }

            if (catalog != null)
            {
                foreach (SoundCatalogEntry entry in catalog.Sounds)
                    AddRow(entry.Name, entry.Plays);
            }

            foreach (KeyValuePair<string, string> pair in config.Sounds)
            {
                RowState row = AddRow(pair.Key, 0);
                row.RelativeTarget = pair.Value;
                row.SourcePath = null;
            }

            RebuildGrid();

            var status = new StringBuilder();
            if (catalog == null)
            {
                status.Append("catalog.json не найден: запустите игру один раз с установленным модом, чтобы собрать список звуков.");
            }
            else
            {
                status.Append("Каталог: ").Append(catalog.Sounds.Count).Append(" звук(ов), собран ")
                      .Append(FormatDate(catalog.GeneratedUtc));
            }

            status.Append("  ·  замен в конфиге: ").Append(config.Sounds.Count);

            if (!string.IsNullOrEmpty(configError))
                status.Append("  ·  конфиг: ").Append(configError);

            SetStatus(status.ToString());
        }

        private RowState AddRow(string soundName, int plays)
        {
            RowState row;
            if (_rows.TryGetValue(soundName, out row))
            {
                if (plays > row.Plays) row.Plays = plays;
                return row;
            }

            row = new RowState { SoundName = soundName, Plays = plays };
            _rows[soundName] = row;
            return row;
        }

        private void RebuildGrid()
        {
            dgvSounds.SuspendLayout();
            try
            {
                dgvSounds.Rows.Clear();

                IEnumerable<RowState> ordered = _rows.Values
                    .OrderByDescending(row => row.Plays)
                    .ThenBy(row => row.SoundName, StringComparer.OrdinalIgnoreCase);

                foreach (RowState state in ordered)
                {
                    int index = dgvSounds.Rows.Add(
                        state.SoundName,
                        state.Plays > 0 ? state.Plays.ToString("N0", CultureInfo.CurrentCulture) : "—",
                        state.DisplayFile,
                        null,
                        null);

                    DataGridViewRow gridRow = dgvSounds.Rows[index];
                    gridRow.Tag = state;
                    gridRow.Cells[colFile.Index].ToolTipText =
                        !string.IsNullOrEmpty(state.SourcePath) ? state.SourcePath : (state.RelativeTarget ?? string.Empty);
                    ApplyRowStyle(gridRow, state);
                }
            }
            finally
            {
                dgvSounds.ResumeLayout();
            }
        }

        // ------------------------------------------------------------- grid events

        private void dgvSounds_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            RowState state = dgvSounds.Rows[e.RowIndex].Tag as RowState;
            if (state == null) return;

            string column = dgvSounds.Columns[e.ColumnIndex].Name;

            if (column == colPick.Name)
                PickFileFor(state);
            else if (column == colClear.Name)
                ClearRow(state);
        }

        private void dgvSounds_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string column = dgvSounds.Columns[e.ColumnIndex].Name;
            if (column == colPick.Name || column == colClear.Name) return;

            RowState state = dgvSounds.Rows[e.RowIndex].Tag as RowState;
            if (state != null) PickFileFor(state);
        }

        private void PickFileFor(RowState state)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Звук для «" + state.SoundName + "»";
                dialog.Filter = "Аудио (*.ogg;*.wav)|*.ogg;*.wav|OGG Vorbis (*.ogg)|*.ogg|WAV (*.wav)|*.wav";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string error;
                if (!SoundFileValidator.TryValidate(dialog.FileName, out error))
                {
                    MessageBox.Show(this, "Файл не подходит:\n\n" + error, "SoundReplacer",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                state.SourcePath = dialog.FileName;
                state.RelativeTarget = null;
                UpdateRowCells(state);
                SetStatus("Выбран файл для «" + state.SoundName + "»: " + Path.GetFileName(dialog.FileName) +
                          ". Нажмите «Применить», чтобы записать конфиг.");
            }
        }

        private void ClearRow(RowState state)
        {
            state.SourcePath = null;
            state.RelativeTarget = null;
            UpdateRowCells(state);
            SetStatus("«" + state.SoundName + "» вернётся к оригинальному звуку после «Применить» и перезапуска игры.");
        }

        private void UpdateRowCells(RowState state)
        {
            DataGridViewRow gridRow = FindGridRow(state);
            if (gridRow == null) return;

            gridRow.Cells[colFile.Index].Value = state.DisplayFile;
            gridRow.Cells[colFile.Index].ToolTipText =
                !string.IsNullOrEmpty(state.SourcePath) ? state.SourcePath : (state.RelativeTarget ?? string.Empty);
            ApplyRowStyle(gridRow, state);
        }

        /// <summary>Зелёным помечаем строки, где выбран файл, но конфиг ещё не записан.</summary>
        private void ApplyRowStyle(DataGridViewRow gridRow, RowState state)
        {
            gridRow.Cells[colFile.Index].Style.ForeColor = string.IsNullOrEmpty(state.SourcePath)
                ? Color.Black
                : Color.FromArgb(0, 110, 0);
        }

        private DataGridViewRow FindGridRow(RowState state)
        {
            foreach (DataGridViewRow row in dgvSounds.Rows)
            {
                if (ReferenceEquals(row.Tag, state)) return row;
            }
            return null;
        }

        // ------------------------------------------------------------- buttons

        private void btnBrowseGame_Click(object sender, EventArgs e)
        {
            string chosen = BrowseForGameDirectory();
            if (chosen != null) SetGameDirectory(chosen, true);
        }

        private void btnOpenModFolder_Click(object sender, EventArgs e)
        {
            if (_paths == null)
            {
                MessageBox.Show(this, "Сначала укажите папку игры.", "SoundReplacer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _paths.OpenPluginFolder();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось открыть папку:\n\n" + ex.Message, "SoundReplacer",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            if (_paths == null)
            {
                ResolveGameDirectory(true);
                return;
            }

            ReloadFromDisk();
            UpdateGameBanner();
        }

        private void btnAddSound_Click(object sender, EventArgs e)
        {
            string name = txtNewSound.Text.Trim();

            if (name.Length == 0)
            {
                SetStatus("Введите имя звука так, как оно называется в игре (точное имя можно посмотреть в catalog.json).");
                txtNewSound.Focus();
                return;
            }

            if (_rows.ContainsKey(name))
            {
                SetStatus("Звук «" + name + "» уже есть в списке.");
                return;
            }

            RowState row = AddRow(name, 0);
            RebuildGrid();
            txtNewSound.Clear();
            SetStatus("Добавлен звук «" + name + "». Выберите для него файл.");

            PickFileFor(row);
        }

        private void btnClearRow_Click(object sender, EventArgs e)
        {
            if (dgvSounds.CurrentRow == null) return;

            RowState state = dgvSounds.CurrentRow.Tag as RowState;
            if (state != null) ClearRow(state);
        }

        // ------------------------------------------------------------- apply

        private void btnApply_Click(object sender, EventArgs e)
        {
            if (_paths == null)
            {
                MessageBox.Show(this, "Сначала укажите папку игры.", "SoundReplacer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!_paths.BepInExInstalled)
            {
                DialogResult answer = MessageBox.Show(
                    this,
                    "В папке игры нет BepInEx — мод не загрузится, пока BepInEx не установлен.\n\n" +
                    "Всё равно записать config.json?",
                    "BepInEx не найден",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (answer != DialogResult.Yes) return;
            }

            List<RowState> toCopy = _rows.Values.Where(row => !string.IsNullOrEmpty(row.SourcePath)).ToList();
            var problems = new List<string>();

            foreach (RowState row in toCopy)
            {
                string error;
                if (!SoundFileValidator.TryValidate(row.SourcePath, out error))
                    problems.Add("• " + row.SoundName + ": " + error);
            }

            if (problems.Count > 0)
            {
                MessageBox.Show(this, "Исправьте файлы:\n\n" + string.Join(Environment.NewLine, problems),
                    "SoundReplacer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _paths.EnsureDirectories();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось создать папку мода:\n\n" + ex.Message, "SoundReplacer",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            btnApply.Enabled = false;
            try
            {
                ReplacerConfig previous = ConfigStore.Load(_paths.ConfigPath, out _);
                var config = new ReplacerConfig();
                var failures = new List<string>();
                int copied = 0;

                foreach (RowState row in _rows.Values.OrderBy(r => r.SoundName, StringComparer.OrdinalIgnoreCase))
                {
                    string relative = null;

                    if (!string.IsNullOrEmpty(row.SourcePath))
                    {
                        try
                        {
                            SoundImportResult result = SoundImporter.Import(row.SoundName, row.SourcePath, _paths.SoundsDirectory);
                            relative = result.RelativePath;
                            if (result.Copied) copied++;

                            row.RelativeTarget = relative;
                            row.SourcePath = null;
                        }
                        catch (Exception ex)
                        {
                            failures.Add("• " + row.SoundName + ": " + ex.Message);
                            continue;
                        }
                    }
                    else if (!string.IsNullOrEmpty(row.RelativeTarget))
                    {
                        string absolute = Path.Combine(_paths.PluginDirectory,
                            row.RelativeTarget.Replace('/', Path.DirectorySeparatorChar));

                        if (!File.Exists(absolute))
                        {
                            failures.Add("• " + row.SoundName + ": файл " + row.RelativeTarget + " пропал, замена убрана");
                            row.RelativeTarget = null;
                            continue;
                        }

                        relative = row.RelativeTarget;
                    }

                    if (!string.IsNullOrEmpty(relative)) config.Sounds[row.SoundName] = relative;
                }

                string saveError;
                if (!ConfigStore.Save(_paths.ConfigPath, config, _paths.BackupConfigPath, out saveError))
                {
                    MessageBox.Show(this, "Не удалось записать config.json:\n\n" + saveError, "SoundReplacer",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                DeleteObsoleteSounds(previous, config);
                RebuildGrid();

                string running = GameProcessWatcher.GetRunningName(_settings.ProcessNames);

                var message = new StringBuilder();
                message.AppendLine("Готово. Записей в config.json: " + config.Sounds.Count + ".");
                if (copied > 0) message.AppendLine("Скопировано новых файлов в Sounds/: " + copied + ".");

                if (failures.Count > 0)
                {
                    message.AppendLine();
                    message.AppendLine("Не удалось применить:");
                    foreach (string failure in failures) message.AppendLine(failure);
                }

                if (running != null)
                {
                    message.AppendLine();
                    message.AppendLine("Игра (" + running + ") сейчас запущена.");
                    message.AppendLine("Изменения вступят в силу только после её перезапуска.");
                }

                MessageBox.Show(this, message.ToString(), "SoundReplacer", MessageBoxButtons.OK,
                    failures.Count > 0 || running != null ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

                SetStatus("Применено записей: " + config.Sounds.Count +
                          (running != null ? " (нужен перезапуск игры)" : "."));
            }
            finally
            {
                btnApply.Enabled = true;
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (_paths == null)
            {
                MessageBox.Show(this, "Сначала укажите папку игры.", "SoundReplacer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult answer = MessageBox.Show(
                this,
                "Сбросить все замены?\n\n" +
                "config.json будет очищен, скопированные звуки удалены.\n" +
                "Оригинальные звуки вернутся после перезапуска игры.",
                "SoundReplacer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes) return;

            ReplacerConfig previous = ConfigStore.Load(_paths.ConfigPath, out _);
            var empty = new ReplacerConfig();

            string saveError;
            if (!ConfigStore.Save(_paths.ConfigPath, empty, _paths.BackupConfigPath, out saveError))
            {
                MessageBox.Show(this, "Не удалось записать config.json:\n\n" + saveError, "SoundReplacer",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DeleteFilesFor(previous);

            foreach (RowState row in _rows.Values)
            {
                row.SourcePath = null;
                row.RelativeTarget = null;
            }

            RebuildGrid();

            string running = GameProcessWatcher.GetRunningName(_settings.ProcessNames);
            string text = "Замены сброшены." +
                          (running != null
                              ? "\n\nИгра запущена — перезапустите её, чтобы вернуть оригинальные звуки."
                              : "\n\nОригинальные звуки вернутся при следующем запуске игры.");

            MessageBox.Show(this, text, "SoundReplacer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetStatus("Сброшено: конфиг пуст, скопированные звуки удалены.");
        }

        // ------------------------------------------------------------- cleanup

        private void DeleteObsoleteSounds(ReplacerConfig previous, ReplacerConfig current)
        {
            var keep = new HashSet<string>(current.Sounds.Values.Select(NormalizeRelative), StringComparer.OrdinalIgnoreCase);

            foreach (string relative in previous.Sounds.Values)
            {
                string normalized = NormalizeRelative(relative);
                if (keep.Contains(normalized)) continue;
                TryDeleteManagedFile(normalized);
            }
        }

        private void DeleteFilesFor(ReplacerConfig config)
        {
            foreach (string relative in config.Sounds.Values)
                TryDeleteManagedFile(NormalizeRelative(relative));
        }

        /// <summary>
        /// Удаляет только файлы внутри Sounds/ — защита от абсолютных путей в конфиге,
        /// чтобы «Сбросить» не мог удалить что-то за пределами папки мода.
        /// </summary>
        private void TryDeleteManagedFile(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;

            try
            {
                string soundsRoot = Path.GetFullPath(_paths.SoundsDirectory) + Path.DirectorySeparatorChar;
                string fullPath = Path.GetFullPath(Path.Combine(_paths.PluginDirectory, relativePath));

                if (!fullPath.StartsWith(soundsRoot, StringComparison.OrdinalIgnoreCase)) return;
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
            catch
            {
                // файл занят/нет прав — не критично, он просто останется лежать
            }
        }

        private static string NormalizeRelative(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return path.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        }

        // ------------------------------------------------------------- misc

        private void UpdateGameBanner()
        {
            _runningGame = _settings.WarnWhenGameRunning
                ? GameProcessWatcher.GetRunningName(_settings.ProcessNames)
                : null;

            if (_runningGame != null)
            {
                lblBanner.Text = "⚠ Игра запущена (" + _runningGame + "). Изменения применятся только после перезапуска.";
                lblBanner.Visible = true;
            }
            else
            {
                lblBanner.Visible = false;
            }
        }

        private static string FormatDate(string iso)
        {
            DateTime parsed;
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
                return parsed.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);

            return string.IsNullOrEmpty(iso) ? "неизвестно" : iso;
        }

        private void SetStatus(string text)
        {
            lblStatus.Text = text ?? string.Empty;
        }
    }
}