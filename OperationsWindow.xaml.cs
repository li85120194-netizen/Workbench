using System.Collections.ObjectModel;
using System.Windows;
namespace Workbench;
public partial class OperationsWindow : Window
{
 public ObservableCollection<OperationLink> Links { get; } = new(OperationsStore.Load().OrderBy(x=>x.SortOrder));
 public OperationsWindow(){InitializeComponent();DataContext=this;}
 private void Add_Click(object sender,RoutedEventArgs e)=>Links.Add(new OperationLink{SortOrder=(Links.Count+1)*10});
 private void Delete_Click(object sender,RoutedEventArgs e){if(LinksGrid.SelectedItem is OperationLink item)Links.Remove(item);}
 private void Save_Click(object sender,RoutedEventArgs e){OperationsStore.Save(Links);System.Windows.MessageBox.Show("Saved locally.","Operations");}
}