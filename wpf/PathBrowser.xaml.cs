using Ookii.Dialogs.Wpf;
using System;
using System.Windows.Controls;

namespace TsActivexGen.Wpf {
    public partial class PathBrowser : DockPanel {
        VistaOpenFileDialog fileDlg = new VistaOpenFileDialog() {
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };
        VistaFolderBrowserDialog folderDlg = new VistaFolderBrowserDialog();

        public PathBrowser() {
            InitializeComponent();

            btnBrowseTypeLibFile.Click += (s, e) => {
                if (Folders) {
                    folderDlg.SelectedPath = Path;
                    if (folderDlg.ShowDialog() ?? false) {
                        Path = folderDlg.SelectedPath;
                    }
                } else { //files
                    fileDlg.FileName = Path;
                    if (fileDlg.ShowDialog() ?? false) {
                        Path = fileDlg.FileName;
                    }
                }
            };


            //TODO Allow modifying the text in the textbox directly
            //  The control should have a dependency property which would raise the SelectionChanged event when the value changes
            //  The DP's value should only be set once the textbox's TextChanged event fires once, and focus has been lost from the textbox
            //      This will prevent the value from being changed while the user is typing

        }
        public string Path {
            get => txbPath.Text;
            set {
                if (value != txbPath.Text) {
                    txbPath.Text = value;
                    SelectionChanged(this, EventArgs.Empty);
                }
            }
        }
        public string Caption {
            get => (string)lbl.Content;
            set => lbl.Content = value;
        }
        public bool Folders { get; set; } = true;
        public event Action<object, EventArgs> SelectionChanged;
    }
}
