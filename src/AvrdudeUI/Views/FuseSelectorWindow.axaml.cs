using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvrdudeUI.Core;

namespace AvrdudeUI.Views;

// Grid of four fuse rows (lfuse / hfuse / efuse / lock) × 8 toggleable bit buttons.
// The layout is generated in code-behind so we don't have to declare 32 buttons and
// 32 name labels in XAML by hand.
//
// Public API mirrors AVRDUDESS's FormFuseSelector:
//   var result = await window.EditFuseAndLocksAsync(mcu, new[] { lf, hf, ef, lb });
//   // result is null on Cancel, or the four new hex strings on OK.
public partial class FuseSelectorWindow : Window
{
    private const int BitCount = 8;
    private static readonly string[] RowNames = { "Low fuse", "High fuse", "Extended fuse", "Lock bits" };

    private readonly Button[,] _bitButtons = new Button[4, BitCount];
    private readonly TextBlock[] _hexReadouts = new TextBlock[4];

    private string[] _newFuses;

    private static readonly IBrush BitOnBrush  = new SolidColorBrush(Color.FromRgb(0x60, 0xa0, 0x40));
    private static readonly IBrush BitOffBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));

    public FuseSelectorWindow() { InitializeComponent(); }

    public async System.Threading.Tasks.Task<string[]> EditFuseAndLocksAsync(Window owner, MCU mcu, string[] fuses)
    {
        Title = $"Fuse and lock bits — {mcu.desc} (signature {mcu.signature.ToUpper()})";

        var supported = FusesList.fl.Items.ContainsKey(mcu.signature);
        var bitNames = supported
            ? FusesList.fl.Items[mcu.signature]
            : new FusesList.FuseBitNames();
        LblUnsupported.IsVisible = !supported;

        var rowLabels = new[] { bitNames.lfd, bitNames.hfd, bitNames.efd, bitNames.lbd };

        // Column headers: bit index labels 7..0 across the top
        for (int col = 0; col < BitCount; col++)
        {
            var header = new TextBlock
            {
                Text = $"bit {7 - col}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 4)
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, col + 1);
            BitsGrid.Children.Add(header);
        }

        // Header for the hex column
        var hexHeader = new TextBlock
        {
            Text = "hex",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(12, 0, 4, 4)
        };
        Grid.SetRow(hexHeader, 0);
        Grid.SetColumn(hexHeader, BitCount + 1);
        BitsGrid.Children.Add(hexHeader);

        for (int row = 0; row < 4; row++)
        {
            // Row label
            var rowLabel = new TextBlock
            {
                Text = RowNames[row],
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 6, 12, 6)
            };
            Grid.SetRow(rowLabel, row + 1);
            Grid.SetColumn(rowLabel, 0);
            BitsGrid.Children.Add(rowLabel);

            // Prefill from incoming hex (falling back to 0xFF if malformed)
            var binary = HexToBinary(row < fuses.Length ? fuses[row] : "FF");

            for (int col = 0; col < BitCount; col++)
            {
                int bitIndex = 7 - col; // Column 0 is the MSB

                var cell = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(2, 2)
                };

                var nameLabel = new TextBlock
                {
                    Text = rowLabels[row][bitIndex] ?? "?",
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2),
                    MaxWidth = 70,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xb0, 0xb0, 0xb0))
                };

                bool enabled = !string.IsNullOrEmpty(rowLabels[row][bitIndex])
                               && rowLabels[row][bitIndex] != "?"
                               && rowLabels[row][bitIndex] != "";
                bool bitSet = binary[7 - bitIndex] == '1';

                var btn = new Button
                {
                    Content = bitSet ? "1" : "0",
                    Width = 42,
                    Height = 34,
                    IsEnabled = enabled,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    FontFamily = FontFamily.Parse("Menlo,Monaco,Consolas,monospace"),
                    Tag = new BitTag(row, bitIndex),
                    Background = bitSet ? BitOnBrush : BitOffBrush
                };
                btn.Click += OnBitClick;

                cell.Children.Add(nameLabel);
                cell.Children.Add(btn);

                Grid.SetRow(cell, row + 1);
                Grid.SetColumn(cell, col + 1);
                BitsGrid.Children.Add(cell);

                _bitButtons[row, bitIndex] = btn;
            }

            // Hex readout
            var readout = new TextBlock
            {
                FontFamily = FontFamily.Parse("Menlo,Monaco,Consolas,monospace"),
                FontSize = 13,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 50
            };
            Grid.SetRow(readout, row + 1);
            Grid.SetColumn(readout, BitCount + 1);
            BitsGrid.Children.Add(readout);
            _hexReadouts[row] = readout;
        }

        RegenerateHexReadouts();

        await ShowDialog(owner);
        return _newFuses; // null if OnCancelClick fired
    }

    private void OnBitClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not BitTag) return;
        var current = (b.Content as string) ?? "0";
        var next = current == "1" ? "0" : "1";
        b.Content = next;
        b.Background = next == "1" ? BitOnBrush : BitOffBrush;
        RegenerateHexReadouts();
    }

    private void RegenerateHexReadouts()
    {
        var newFuses = new string[4];
        for (int row = 0; row < 4; row++)
        {
            // Read MSB → LSB into a binary string
            var bin = "";
            for (int bit = 7; bit >= 0; bit--)
                bin += (_bitButtons[row, bit].Content as string) ?? "0";

            var hex = BinaryToHex(bin);
            _hexReadouts[row].Text = $"0x{hex}";
            newFuses[row] = hex;
        }
        _newFuses = newFuses;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        // _newFuses already holds the latest state via RegenerateHexReadouts.
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _newFuses = null;
        Close();
    }

    private static string BinaryToHex(string binary) =>
        Convert.ToInt32(binary, 2).ToString("X2");

    private static string HexToBinary(string hex)
    {
        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            value = 0xFF;
        return Convert.ToString(value, 2).PadLeft(8, '0');
    }

    private readonly record struct BitTag(int Row, int BitIndex);
}
