﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Artorio
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string SAVE_FILE = "cfg.bin";

        private FactorioBPMaker bpMaker;
        private OpenFileDialog openFileDialog;

        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;
            bpMaker = new FactorioBPMaker();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Load data fail", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (File.Exists(SAVE_FILE))
                    File.Delete(SAVE_FILE);
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                SaveData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Save data fail", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadData()
        {
            if (!File.Exists(SAVE_FILE))
                return;

            using (FileStream stream = File.Open(SAVE_FILE, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var br = new BinaryReader(stream))
            {
                inputPath.Text = br.ReadString();

                int confCount = br.ReadInt32();

                for (int i = 0; i < confCount; i++)
                {
                    bool useRangeColor = br.ReadBoolean();
                    var colorFrom = Color.FromRgb(br.ReadByte(), br.ReadByte(), br.ReadByte());
                    var colorTo = Color.FromRgb(br.ReadByte(), br.ReadByte(), br.ReadByte());
                    string itemName = br.ReadString();

                    AddConfig(new FilterConfig(useRangeColor, colorFrom, colorTo, itemName));
                }
            }
        }

        private void SaveData()
        {
            using (FileStream stream = File.Open(SAVE_FILE, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(stream))
            {
                bw.Write(inputPath.Text);

                int confCount = configStack.Children.Count - 1;
                bw.Write(confCount);

                if (confCount > 0)
                {
                    foreach (ColorItemCast cc in ForeachColorItemCast())
                    {
                        IReadOnlyFilterConfig conf = cc.GetFilterConfig;
                        bw.Write(conf.UseRangeColor);

                        bw.Write(conf.FromColor.R);
                        bw.Write(conf.FromColor.G);
                        bw.Write(conf.FromColor.B);

                        bw.Write(conf.ToColor.R);
                        bw.Write(conf.ToColor.G);
                        bw.Write(conf.ToColor.B);

                        bw.Write(conf.ItemName);
                    }
                }
            }
        }

        private IEnumerable<ColorItemCast> ForeachColorItemCast()
        {
            if (configStack.Children.Count == 1)
                yield break;

            for (int i = 0; i < configStack.Children.Count - 1; i++)
            {
                // TODO: Optimize
                yield return (ColorItemCast)configStack.Children[i];
            }
        }

        private void AddConfigClick(object sender, RoutedEventArgs e)
        {
            AddConfig(null);
        }

        private void AddConfig(FilterConfig filterConfig)
        {
            var cc = new ColorItemCast(filterConfig)
            {
                Width = 640,
                Height = 40,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            cc.OnRemoveThisClick += ColorItemCast_OnRemoveThisClick;
            configStack.Children.Insert(configStack.Children.Count - 1, cc);
        }

        private void ColorItemCast_OnRemoveThisClick(int id)
        {
            int index = -1;
            foreach (ColorItemCast cc in ForeachColorItemCast())
            {
                index++;
                if (cc.ID == id)
                {
                    configStack.Children.RemoveAt(index);
                    break;
                }
            }
        }

        private void SelectImageClick(object sender, RoutedEventArgs e)
        {
            if (openFileDialog == null)
            {
                openFileDialog = new OpenFileDialog
                {
                    Filter = "PNG file | *.png",
                    CheckFileExists = true,
                    CheckPathExists = true,
                    Title = "Select png file",
                    InitialDirectory = Environment.CurrentDirectory,
                };
            }

            bool? isOk = openFileDialog.ShowDialog();
            if (isOk.Value)
            {
                inputPath.Text = openFileDialog.FileName;
            }
        }

        private async void ConvertAndCopyClick(object sender, RoutedEventArgs e)
        {
            // check
            if (string.IsNullOrWhiteSpace(inputPath.Text) || configStack.Children.Count == 1)
                return;

            if (!File.Exists(inputPath.Text))
            {
                MessageBox.Show("File '" + inputPath.Text + "' not exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // parse
            mainGrid.IsEnabled = false;
            progress.IsIndeterminate = true;
            string path = inputPath.Text;

            await Task.Factory.StartNew(new Action(() => CreateBlueprint(path)));

            progress.IsIndeterminate = false;
            mainGrid.IsEnabled = true;
        }

        private void CreateBlueprint(string pngPath)
        {
            // in Task
            try
            {
                bpMaker.ClearFilterConfig();

                Dispatcher.Invoke(() =>
                {
                    foreach (ColorItemCast cc in ForeachColorItemCast())
                    {
                        bpMaker.AddFilterConfig(cc.GetFilterConfig);
                    }
                });

                string result = bpMaker.CreateBlueprint(pngPath, out int insertItems);

                if (result == null)
                {
                    MessageBox.Show("Filter fail", "Fail", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    Dispatcher.Invoke(() => Clipboard.SetText(result, TextDataFormat.Text));
                    MessageBox.Show("Success. Items: " + insertItems.ToString(), "OK", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
