using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace VideoEditor.Controls
{
    public partial class PhotoTextOverlayControl : UserControl
    {
         private string? _selectedFilePath; 

        public PhotoTextOverlayControl()
        {
            InitializeComponent();
            ApplySelectedFont(); // Применяем начальный шрифт при старте
             // ВКЛЮЧАЕМ ЗАГЛАВНЫЕ ПО УМОЛЧАНИЮ ПРИ СТАРТЕ:
    InputTextBox.CharacterCasing = CharacterCasing.Upper;
    InputTextBox.Text = InputTextBox.Text.ToUpper();
        }

        private void SelectPhoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Изображения (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedFilePath = openFileDialog.FileName;
                
                BitmapImage bitmap = new BitmapImage(new Uri(_selectedFilePath));
                TargetImage.Source = bitmap;

                PlaceholderText.Visibility = Visibility.Collapsed;
                OverlayTextBlock.Visibility = Visibility.Visible;

                PhotoArea.UpdateLayout();

                double maxW = bitmap.PixelWidth > 0 ? bitmap.PixelWidth / 2.0 : 500;
                double maxH = bitmap.PixelHeight > 0 ? bitmap.PixelHeight / 2.0 : 500;

                SliderX.Minimum = -maxW;
                SliderX.Maximum = maxW;
                SliderY.Minimum = -maxH;
                SliderY.Maximum = maxH;

                SliderX.Value = 0;
                SliderY.Value = 0;
            }
        }

        // Логика переключения шрифтов
        private void ComboFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySelectedFont();
        }

    private void ApplySelectedFont()
{
    if (OverlayTextBlock == null || ComboFont == null) return;

    switch (ComboFont.SelectedIndex)
    {
        case 0:
            // Подключаем ваш шрифт из папки fonts
            // Важно: после знака # пишется системное имя шрифта (для Impact это Impact)
            OverlayTextBlock.FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./fonts/#Impact");
            break;
       
        case 1:
            OverlayTextBlock.FontFamily = new FontFamily("Arial");
            break;
        case 2:
            OverlayTextBlock.FontFamily = new FontFamily("Times New Roman");
            break;
        case 3:
            OverlayTextBlock.FontFamily = new FontFamily("Comic Sans MS");
            break;
    }
}

// Метод переключения регистра текста (ЗАГЛАВНЫЕ / обычные)
// Переключение регистра на лету
private void CheckCaps_Toggle(object sender, RoutedEventArgs e)
{
    if (InputTextBox == null || CheckCaps == null) return;

    if (CheckCaps.IsChecked == true)
    {
        // Заставляем TextBox автоматически переводить всё в ЗАГЛАВНЫЕ при вводе
        InputTextBox.CharacterCasing = CharacterCasing.Upper;
        // Сразу переводим уже написанный текст в верхний регистр
        InputTextBox.Text = InputTextBox.Text.ToUpper();
    }
    else
    {
        // Возвращаем обычный режим (Normal)
        InputTextBox.CharacterCasing = CharacterCasing.Normal;
    }
}

// Новый метод для выравнивания строк внутри текста
private void ComboTextAlignment_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (OverlayTextBlock == null || ComboTextAlignment == null) return;

    switch (ComboTextAlignment.SelectedIndex)
    {
        case 0:
            OverlayTextBlock.TextAlignment = TextAlignment.Left;
            break;
        case 1:
            OverlayTextBlock.TextAlignment = TextAlignment.Center;
            break;
        case 2:
            OverlayTextBlock.TextAlignment = TextAlignment.Right;
            break;
    }
}


        private void ComboAlignment_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OverlayTextBlock == null) return;

            if (SliderX != null) SliderX.Value = 0;
            if (SliderY != null) SliderY.Value = 0;

            switch (ComboAlignment.SelectedIndex)
            {
                case 0: OverlayTextBlock.HorizontalAlignment = HorizontalAlignment.Left; OverlayTextBlock.VerticalAlignment = VerticalAlignment.Top; break;
                case 1: OverlayTextBlock.HorizontalAlignment = HorizontalAlignment.Center; OverlayTextBlock.VerticalAlignment = VerticalAlignment.Top; break;
                case 2: OverlayTextBlock.HorizontalAlignment = HorizontalAlignment.Right; OverlayTextBlock.VerticalAlignment = VerticalAlignment.Top; break;
                case 3: OverlayTextBlock.HorizontalAlignment = HorizontalAlignment.Left; OverlayTextBlock.VerticalAlignment = VerticalAlignment.Center; break;
                case 4: OverlayTextBlock.HorizontalAlignment = HorizontalAlignment.Center; OverlayTextBlock.VerticalAlignment = VerticalAlignment.Center; break;
                case 5: OverlayTextBlock.HorizontalAlignment = HorizontalAlignment.Right; OverlayTextBlock.VerticalAlignment = VerticalAlignment.Center; break;
                case 6: OverlayTextBlock.HorizontalAlignment = HorizontalAlignment.Left; OverlayTextBlock.VerticalAlignment = VerticalAlignment.Bottom; break;
                case 7: OverlayTextBlock.HorizontalAlignment = HorizontalAlignment.Center; OverlayTextBlock.VerticalAlignment = VerticalAlignment.Bottom; break;
                case 8: OverlayTextBlock.HorizontalAlignment = HorizontalAlignment.Right; OverlayTextBlock.VerticalAlignment = VerticalAlignment.Bottom; break;
            }
        }

        private void SavePhoto_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                MessageBox.Show("Сначала выберите изображение!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg",
                FileName = "result_image"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    RenderTargetBitmap renderTarget = new RenderTargetBitmap(
                        (int)PhotoArea.ActualWidth, 
                        (int)PhotoArea.ActualHeight, 
                        96, 96, PixelFormats.Pbgra32);
                    
                    renderTarget.Render(PhotoArea);

                    BitmapEncoder encoder = saveFileDialog.FilterIndex == 1 ? new PngBitmapEncoder() : new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                    using (FileStream stream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    {
                        encoder.Save(stream);
                    }

                    MessageBox.Show("Изображение успешно сохранено!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
