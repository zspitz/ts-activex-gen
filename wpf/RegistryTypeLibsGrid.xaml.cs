using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using TsActivexGen.tlibuilder;
using TsActivexGen.Util;
using static System.StringComparison;
using static System.Windows.Controls.Primitives.Selector;

namespace TsActivexGen.Wpf {
    public partial class RegistryTypeLibsGrid {
        public RegistryTypeLibsGrid() {
            InitializeComponent();

            //TODO enable sorting on this datagrid such that selected items are always on top

            Loaded += (s, e) => {
                dgTypeLibs.ItemsSource = TypeLibDetails.FromRegistry.Value;
                TypeLibDetails.FromRegistry.Value.ForEach(x => x.PropertyChanged += (s1, e1) => {
                    if (e1.PropertyName != "Selected") { return; }
                    TypeLibDetails[] added = new TypeLibDetails[] { };
                    TypeLibDetails[] removed = new TypeLibDetails[] { };
                    if (x.Selected) {
                        added = new[] { x };
                    } else {
                        removed = new[] { x };
                    }
                    SelectionDataChanged?.Invoke(this, new SelectionChangedEventArgs(SelectionChangedEvent,removed ,  added));
                });

                txbFilter.TextChanged += (s1, e1) =>
                    CollectionViewSource.GetDefaultView(dgTypeLibs.ItemsSource).Filter = x => {
                        var details = x as TypeLibDetails;
                        return details.Selected || (details.Name ?? "").Contains(txbFilter.Text, OrdinalIgnoreCase);
                    };
            };
        }

        public List<TypeLibDetails> Items => dgTypeLibs.Items<TypeLibDetails>().Where(x => x.Selected).ToList();

        public event SelectionChangedEventHandler SelectionDataChanged;
    }
}
