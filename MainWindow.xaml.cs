using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EasyResamp;

// Zostawiamy tylko podstawowe systemowe usingi, resztę wpisujemy w kodzie "z palca"
// żeby nie było konfliktów między WPF a WinForms.

namespace EasyResampWPF
{
    public class PlikObrazu
    {
        public string NazwaPliku { get; set; }
        public string SciezkaPelna { get; set; }
    }

    public partial class MainWindow : System.Windows.Window
    {
        public ObservableCollection<PlikObrazu> ListaPlikow { get; set; } = new ObservableCollection<PlikObrazu>();
        private readonly string[] dozwolone = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff" };
        private UserSettings settings;
        private bool isInitializing = true;

        public MainWindow()
        {
            InitializeComponent();
            lstFiles.ItemsSource = ListaPlikow;

            settings = UserSettings.Load();
            ApplySettingsToUI();
            isInitializing = false;
        }

        private void ApplySettingsToUI()
        {
            txtWidth.Text = settings.DefaultWidth.ToString();
            txtHeight.Text = settings.DefaultHeight.ToString();

            if (settings.UseFixedPath && Directory.Exists(settings.OutputPath))
            {
                // Najpierw tekst, potem checkbox - naprawia problem otwierania okna przy starcie
                txtFixedPath.Text = settings.OutputPath;
                txtFixedPath.Opacity = 1.0;
                rbFixed.IsChecked = true;
            }
            else
            {
                rbAsk.IsChecked = true;
                txtFixedPath.Opacity = 0.5;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (int.TryParse(txtWidth.Text, out int w)) settings.DefaultWidth = w;
            if (int.TryParse(txtHeight.Text, out int h)) settings.DefaultHeight = h;

            settings.UseFixedPath = rbFixed.IsChecked == true;
            settings.OutputPath = txtFixedPath.Text;
            settings.Save();
        }

        // --- DRAG & DROP (Pełne ścieżki do WPF) ---
        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] pliki = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                DodajPliki(pliki);
            }
        }

        // --- DODAWANIE PLIKÓW ---

        // Obsługa kliknięcia (RoutedEventArgs - WPF)
        private void BtnAddFiles_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            WybierzPlikiDialog();
        }

        // Obsługa kliknięcia myszą na Border (MouseButtonEventArgs - WPF Input)
        private void BtnAddFiles_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            WybierzPlikiDialog();
        }

        private void WybierzPlikiDialog()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Filter = "Obrazy|*.jpg;*.jpeg;*.png;*.bmp";

            if (ofd.ShowDialog() == true)
            {
                DodajPliki(ofd.FileNames);
            }
        }

        private void DodajPliki(string[] sciezki)
        {
            foreach (var sciezka in sciezki)
            {
                string ext = Path.GetExtension(sciezka).ToLower();
                if (dozwolone.Contains(ext) && !ListaPlikow.Any(x => x.SciezkaPelna == sciezka))
                {
                    ListaPlikow.Add(new PlikObrazu
                    {
                        NazwaPliku = Path.GetFileName(sciezka),
                        SciezkaPelna = sciezka
                    });
                }
            }
            AktualizujStatus();
        }

        // --- USUWANIE ELEMENTÓW (Pełna ścieżka do Button) ---
        private void BtnRemoveItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Tutaj był konflikt, teraz wymuszamy Button z WPF
            System.Windows.Controls.Button btn = sender as System.Windows.Controls.Button;
            string sciezka = btn.Tag.ToString();

            var doUsuniecia = ListaPlikow.FirstOrDefault(x => x.SciezkaPelna == sciezka);
            if (doUsuniecia != null)
            {
                ListaPlikow.Remove(doUsuniecia);
                AktualizujStatus();
            }
        }

        // --- OBSŁUGA FOLDERÓW ---
        private void FolderOption_Changed(object sender, System.Windows.RoutedEventArgs e)
        {
            if (isInitializing) return;
            if (txtFixedPath == null) return;

            bool isFixed = rbFixed.IsChecked == true;
            txtFixedPath.Opacity = isFixed ? 1.0 : 0.5;

            if (isFixed && string.IsNullOrEmpty(txtFixedPath.Text))
            {
                WybierzFolder();
            }
        }

        private void BtnBrowseFolder_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            WybierzFolder();
        }

        private void WybierzFolder()
        {
            // Tutaj wymuszamy użycie WinForms tylko do Dialogu
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtFixedPath.Text = fbd.SelectedPath;
                    rbFixed.IsChecked = true;
                }
            }
        }

        // --- START (Tutaj używamy System.Drawing do grafiki) ---
        private async void BtnStart_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // MessageBox z WPF
            if (ListaPlikow.Count == 0) { System.Windows.MessageBox.Show("Lista pusta!"); return; }

            if (!int.TryParse(txtWidth.Text, out int w) || !int.TryParse(txtHeight.Text, out int h))
            { System.Windows.MessageBox.Show("Błędne wymiary!"); return; }

            string folderOut = "";

            if (rbFixed.IsChecked == true)
            {
                if (string.IsNullOrEmpty(txtFixedPath.Text))
                {
                    System.Windows.MessageBox.Show("Wybierz folder!");
                    return;
                }
                folderOut = txtFixedPath.Text;
            }
            else
            {
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                {
                    if (fbd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    folderOut = fbd.SelectedPath;
                }
            }

            progressBar.Value = 0;
            progressBar.Maximum = ListaPlikow.Count;
            lblStatus.Text = "Przetwarzanie...";

            await Task.Run(() =>
            {
                int i = 0;
                foreach (var plik in ListaPlikow)
                {
                    try
                    {
                        // Pełne ścieżki do System.Drawing (Grafika, Bitmapa, Obraz)
                        using (System.Drawing.Image img = System.Drawing.Image.FromFile(plik.SciezkaPelna))
                        using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(w, h))
                        {
                            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                            {
                                // Pełne ścieżki do enumów jakości
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                                g.DrawImage(img, 0, 0, w, h);
                            }

                            string name = $"{Path.GetFileNameWithoutExtension(plik.NazwaPliku)}_{w}x{h}.jpg";
                            bmp.Save(Path.Combine(folderOut, name), System.Drawing.Imaging.ImageFormat.Jpeg);
                        }
                    }
                    catch { /* Ignorujemy błędy */ }

                    i++;
                    Dispatcher.Invoke(() =>
                    {
                        progressBar.Value = i;
                        lblStatus.Text = $"Zrobiono {i} z {ListaPlikow.Count}";
                    });
                }
            });

            lblStatus.Text = "Gotowe!";
            System.Windows.MessageBox.Show("Zakończono!");
            System.Diagnostics.Process.Start("explorer.exe", folderOut);
        }

        private void AktualizujStatus()
        {
            lblStatus.Text = $"Plików: {ListaPlikow.Count}";
        }
    }
}