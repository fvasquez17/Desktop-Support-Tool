// Resolve ambiguity between WPF and WinForms types when UseWindowsForms=true.
// WPF types take priority since this is a WPF application.
// WinForms is only used for the NotifyIcon system tray integration.

global using Application = System.Windows.Application;
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Button = System.Windows.Controls.Button;
global using Clipboard = System.Windows.Clipboard;
global using Color = System.Windows.Media.Color;
global using ComboBox = System.Windows.Controls.ComboBox;
global using FontFamily = System.Windows.Media.FontFamily;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using MessageBox = System.Windows.MessageBox;
global using MessageBoxResult = System.Windows.MessageBoxResult;
global using Orientation = System.Windows.Controls.Orientation;
global using Point = System.Windows.Point;
global using RadioButton = System.Windows.Controls.RadioButton;
global using Size = System.Windows.Size;
global using TextBox = System.Windows.Controls.TextBox;
global using Timer = System.Threading.Timer;
global using UserControl = System.Windows.Controls.UserControl;
global using VerticalAlignment = System.Windows.VerticalAlignment;
global using Window = System.Windows.Window;
