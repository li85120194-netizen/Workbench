using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
namespace Workbench;
public partial class IndustryWindow : Window
{
 public IndustryWindow(){InitializeComponent();Loaded+=(_,_)=>RefreshLinks();}
 void RefreshLinks()=>LinksItems.ItemsSource=OperationsStore.Load().Where(x=>x.Enabled).OrderBy(x=>x.SortOrder).ToList();
 void Link_Click(object sender,RoutedEventArgs e){if(sender is not System.Windows.Controls.Button{Tag:string url}||!Uri.TryCreate(url,UriKind.Absolute,out _))return;Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
 void Configure_Click(object sender,RoutedEventArgs e){new OperationsWindow{Owner=this}.ShowDialog();RefreshLinks();}
}