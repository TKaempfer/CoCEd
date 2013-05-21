﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CoCEd.Model;
using Microsoft.Win32;

namespace CoCEd.ViewModel
{
    public static class FileManagerVM
    {
        public static IEnumerable<IMenuVM> GetOpenMenus()
        {
            foreach (var dir in FileManager.GetDirectories())
            {
                yield return new SourceDirectoryVM(dir);
            }
            yield return new ImportRootVM();
        }

        public static IEnumerable<IMenuVM> GetSaveMenus()
        {
            foreach (var dir in FileManager.GetDirectories())
            {
                yield return new TargetDirectoryVM(dir);
            }
            yield return new ExportRootVM();
        }
    }

    public interface IMenuVM
    {
        String Label { get; }
        Brush Foreground { get; }
        IEnumerable<Object> Children { get; }
        bool HasSeparatorBefore { get; }
        bool IsVisible { get; }
        void OnClick();
    }

    public interface IMenuItemVM
    {
        string Path { get; }
        string Label { get; }
        string SubLabel { get; }
        Brush Foreground { get; }
        Image Icon { get; }
        Visibility SubLabelVisibility { get; }
        void OnClick();
    }

    public sealed class SourceDirectoryVM : IMenuVM
    {
        readonly FlashDirectory _directory;

        public SourceDirectoryVM(FlashDirectory directory)
        {
            _directory = directory;
        }

        public string Label
        {
            get { return _directory.Name; }
        }

        public IEnumerable<Object> Children
        {
            get { return _directory.Files.Select(x => new FileVM(x, _directory.IsExternal, true)); }
        }

        public bool HasSeparatorBefore
        {
            get { return _directory.HasSeparatorBefore; }
        }

        public bool IsVisible
        {
            get { return _directory.Files.Count != 0; }
        }

        public Brush Foreground
        {
            get { return Brushes.Black; }
        }

        public void OnClick()
        {
        }
    }

    public sealed class TargetDirectoryVM : IMenuVM
    {
        readonly FlashDirectory _directory;

        public TargetDirectoryVM(FlashDirectory directory)
        {
            _directory = directory;
        }

        public string Label
        {
            get { return _directory.Name; }
        }

        public bool HasSeparatorBefore
        {
            get { return _directory.HasSeparatorBefore; }
        }

        public bool IsVisible
        {
            get { return _directory.Files.Count != 0 || !String.IsNullOrEmpty(_directory.Path); }
        }

        public Brush Foreground
        {
            get { return _directory.Files.Count == 0 ? Brushes.DarkGray : Brushes.Black; }
        }

        public IEnumerable<Object> Children
        {
            get
            {
                // External
                if (_directory.IsExternal)
                {
                    foreach (var file in _directory.Files) yield return new FileVM(file, _directory.IsExternal, false);
                }

                // Path not found or external?
                if (String.IsNullOrEmpty(_directory.Path)) yield break;

                // Return either a SaveTargetVM or a FileVM for every slot
                for (int i = 1; i <= 10; i++)
                {
                    var name = "Coc_" + i + ".sol";
                    var file = _directory.Files.FirstOrDefault(x => x.FilePath.EndsWith(name, StringComparison.InvariantCultureIgnoreCase));
                    if (file != null)
                    {
                        yield return new FileVM(file, _directory.IsExternal, false);
                    }
                    else
                    {
                        var path = Path.Combine(_directory.Path, name);
                        var target = new SaveSlotVM { Label = "Coc_" + i, Path = path };
                        yield return target;
                    }
                }
            }
        }

        public void OnClick()
        {
        }
    }

    public sealed class ImportRootVM : IMenuVM
    {
        public ImportRootVM()
        {
        }

        public string Label
        {
            get { return "Import"; }
        }

        public IEnumerable<Object> Children
        {
            get { yield break; }
        }

        public bool HasSeparatorBefore
        {
            get { return false; }
        }

        public bool IsVisible
        {
            get { return true; }
        }

        public Brush Foreground
        {
            get { return Brushes.Black; }
        }

        public void OnClick()
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Flash objects (.sol)|*.sol|All files|*.*";
            dlg.DefaultExt = ".sol";
            dlg.CheckFileExists = true;
            dlg.Multiselect = false;
            dlg.RestoreDirectory = true;

            var result = dlg.ShowDialog();
            if (result == false) return;

            string path = dlg.FileName;
            VM.Instance.Load(path);
        }
    }

    public sealed class ExportRootVM : IMenuVM
    {
        public ExportRootVM()
        {
        }

        public string Label
        {
            get { return "Export"; }
        }

        public IEnumerable<Object> Children
        {
            get { yield break; }
        }

        public bool HasSeparatorBefore
        {
            get { return false; }
        }

        public bool IsVisible
        {
            get { return true; }
        }

        public Brush Foreground
        {
            get { return Brushes.Black; }
        }

        public void OnClick()
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "CoC slot (.sol)|*.sol|CoC exported file|*.*";
            dlg.DefaultExt = ".sol";
            dlg.AddExtension = true;
            dlg.OverwritePrompt = true;
            dlg.RestoreDirectory = true;
            dlg.ValidateNames = true;

            var result = dlg.ShowDialog();
            if (result == false) return;

            string path = dlg.FileName;
            var format = (SerializationFormat)dlg.FilterIndex;
            VM.Instance.Save(path, format);
        }
    }

    public class FileVM : IMenuItemVM
    {
        readonly bool _isExternal;
        readonly bool _openOnClick;

        public FileVM(AmfFile source, bool isExternal, bool openOnClick)
        {
            Source = source;
            _isExternal = isExternal;
            _openOnClick = openOnClick;
        }

        public AmfFile Source 
        { 
            get; 
            private set; 
        }

        public string Path
        {
            get { return Source.FilePath; }
        }

        public string Label
        {
            get { return _isExternal ? System.IO.Path.GetFileNameWithoutExtension(Source.FilePath) : Source.Name; }
        }

        public string SubLabel
        {
            get { return Source["short"] + " - " + Source["days"] + " days" + " - " + Elapsed(); }
        }

        string Elapsed()
        {
            var elapsed = DateTime.Now - Source.Date;
            if (elapsed.TotalDays > 1) return (int)elapsed.TotalDays + " days ago";
            if (elapsed.TotalHours > 1) return (int)elapsed.TotalHours + " hours ago";
            if (elapsed.TotalMinutes > 1) return (int)elapsed.TotalMinutes + " minutes ago";
            return "1 minute ago";
        }

        public Brush Foreground
        {
            get { return Brushes.Black; }
        }

        public Visibility SubLabelVisibility
        {
            get { return Visibility.Visible; }
        }

        public Image Icon
        {
            get
            {
                if (String.IsNullOrEmpty(Source.Error)) return null;

                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri("pack://application:,,,/assets/cross.png", UriKind.Absolute);
                bmp.EndInit();

                var img = new Image();
                img.Source = bmp;
                return img;
            }
        }

        public void OnClick()
        {
            if (_openOnClick) VM.Instance.Load(Source.FilePath);
            else VM.Instance.Save(Source.FilePath, Source.Format);
        }
    }

    public class SaveSlotVM : IMenuItemVM
    {
        public SerializationFormat Format
        {
            get { return SerializationFormat.Slot; }
        }

        public string Path
        {
            get;
            set;
        }

        public string Label
        {
            get;
            set;
        }

        public string SubLabel
        {
            get;
            set;
        }

        public Image Icon
        {
            get { return null; }
        }

        public Brush Foreground
        {
            get { return Brushes.DarkGray; }
        }

        public Visibility SubLabelVisibility
        {
            get { return Visibility.Collapsed; }
        }

        void IMenuItemVM.OnClick()
        {
            VM.Instance.Save(Path, SerializationFormat.Slot);
        }
    }
}
