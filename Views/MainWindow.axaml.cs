using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace FileCrypto.Views;

public partial class MainWindow : Window
{
    private const int IV_SIZE = 16;
    public IStorageFile? KeyFile { get; set; }
    public IStorageFile? ChosenFile { get; set; }
    public IStorageFile? DestinationFile { get; set; }
    public byte[] RawKey { get; set; }
    public string HexKey => Convert.ToHexString(RawKey);

    public MainWindow()
    {
        InitializeComponent();
        KeyFile = null;
        ChosenFile = null;
        RawKey = Array.Empty<byte>();
    }

    private async void ChooseFileOnClick(object? sender, RoutedEventArgs e)
    {
        var pickedFiles = await this.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { Title = "Choose a file:", AllowMultiple = false }
        );

        if (pickedFiles.Count != 1)
            return;

        ChosenFile = pickedFiles[0];
        ChosenLabel.Content = ChosenFile.Name;
    }

    private async void ChooseDestinationOnClick(object? sender, RoutedEventArgs e)
    {
        var pickedFiles = await this.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions() { Title = "Choose a file:" }
        );

        if (pickedFiles is null)
            return;

        DestinationFile = pickedFiles;
        DestinationLabel.Content = DestinationFile.Name;
    }

    private async void ChooseKeyOnClick(object? sender, RoutedEventArgs e)
    {
        var pickedFiles = await this.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { Title = "Choose a key file:", AllowMultiple = false }
        );

        if (pickedFiles.Count != 1)
            return;

        KeyFile = pickedFiles[0];
        KeyLabel.Content = KeyFile.Name;
        await using var keyReader = await KeyFile.OpenReadAsync();
        var key = new byte[64];
        var bytesRead = await keyReader.ReadAsync(key);
        if (bytesRead != 32 && bytesRead != 48 && bytesRead != 64)
            return;
        var hexKey = Encoding.UTF8.GetString(key.Take(bytesRead).ToArray());
        RawKey = Convert.FromHexString(hexKey);
        KeyTextBox.Text = HexKey;
    }

    private void ShowKeyOnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox showKeyCheckBox)
        {
            KeyTextBox.RevealPassword = showKeyCheckBox.IsChecked ?? false;
        }
    }

    private async void EncryptOnClick(object? sender, RoutedEventArgs e)
    {
        if (ChosenFile is null || DestinationFile is null)
            return;
        var aes = Aes.Create();
        var iv = RandomNumberGenerator.GetBytes(IV_SIZE);
        var encryptor = aes.CreateEncryptor(RawKey, iv);
        await using var reader = await ChosenFile.OpenReadAsync();
        await using var writer = await DestinationFile.OpenWriteAsync();
        await using var cryptoStream = new CryptoStream(writer, encryptor, CryptoStreamMode.Write);
        await writer.WriteAsync(iv);
        await reader.CopyToAsync(cryptoStream);
    }

    private async void DecryptOnClick(object? sender, RoutedEventArgs e)
    {
        if (ChosenFile is null || DestinationFile is null)
            return;
        var aes = Aes.Create();
        await using var reader = await ChosenFile.OpenReadAsync();
        var iv = new byte[IV_SIZE];
        var bytesRead = await reader.ReadAsync(iv);
        if (bytesRead != IV_SIZE)
        {
            Debug.WriteLine("Did not read amount of bytes, required for IV.");
            return;
        }

        var decryptor = aes.CreateDecryptor(RawKey, iv);
        await using var writer = await DestinationFile.OpenWriteAsync();
        await using var cryptoStream = new CryptoStream(writer, decryptor, CryptoStreamMode.Write);
        await reader.CopyToAsync(cryptoStream);
    }

    private void GenerateKeyOnClick(object? sender, RoutedEventArgs e)
    {
        var newKey = RandomNumberGenerator.GetBytes(32);
        RawKey = newKey;
        KeyTextBox.Text = HexKey;
    }

    private void SaveKeyOnClick(object? sender, RoutedEventArgs e)
    {
        File.WriteAllText("FileCrypto.key", HexKey);
    }

    private void KeyOnChanged(object? sender, TextChangedEventArgs e)
    {
        if (KeyTextBox.Text is null)
            return;
        RawKey = Convert.FromHexString(KeyTextBox.Text);
    }
}
