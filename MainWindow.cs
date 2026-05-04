using System.Windows;
using Microsoft.Win32; // Для OpenFileDialog
using Xabe.FFmpeg;
namespace VideoEditor;

public partial class MainWindow : Window
{
     // Поля для хранения путей (один раз!)
   // 1. ОБЪЯВЛЯЕМ ПЕРЕМЕННЫЕ ЗДЕСЬ (чтобы их видели все методы)
    double videoDuration = 0;
    double audioDuration = 0;
   
    string videoPath = "";
    string audioPath = "";
    bool isLoadingFiles = false; // Флаг: идет ли сейчас процесс загрузки файлов
    System.Windows.Threading.DispatcherTimer timer;
bool isDragging = false; // Чтобы слайдер не "дергался", когда мы его тянем рукой
    public MainWindow()
    {
        InitializeComponent();
           timer = new System.Windows.Threading.DispatcherTimer();
    timer.Interval = TimeSpan.FromMilliseconds(100); // Обновлять 10 раз в секунду
    timer.Tick += Timer_Tick;
            Xabe.FFmpeg.FFmpeg.SetExecutablesPath(@"C:\ffmpeg\bin"); 


        // Укажите путь к папке, где лежит ffmpeg.exe, если он не добавлен в PATH системы
        // FFmpeg.SetExecutablesPath(@"C:\ffmpeg\bin"); 
    }
  private void Timer_Tick(object? sender, EventArgs e)
{
    if (ResultPreview.Source != null && ResultPreview.NaturalDuration.HasTimeSpan && !isDragging)
    {
        TimelineSlider.Maximum = ResultPreview.NaturalDuration.TimeSpan.TotalSeconds;
        TimelineSlider.Value = ResultPreview.Position.TotalSeconds;
        
        CurrentTimeText.Text = ResultPreview.Position.ToString(@"mm\:ss");
        TotalTimeText.Text = ResultPreview.NaturalDuration.TimeSpan.ToString(@"mm\:ss");

        // ПРОВЕРКА НА КОНЕЦ ВИДЕО
        if (ResultPreview.Position >= ResultPreview.NaturalDuration.TimeSpan)
        {
            StopAllPlayback();
        }
    }
}
private void StopAllPlayback()
{
    timer.Stop();
    ResultPreview.Pause();
    AudioPreview.Pause();
    
    // Сбрасываем позиции в начало
    ResultPreview.Position = TimeSpan.Zero;
    // Аудио возвращаем к выбранному началу на слайдере
    AudioPreview.Position = TimeSpan.FromSeconds(StartSlider.Value);
    
    // Обнуляем ползунок визуально
    TimelineSlider.Value = 0;
    
    StatusText.Text = "Просмотр завершен";
}
private void ResultPreview_MediaEnded(object sender, RoutedEventArgs e)
{
    StopAllPlayback();
}

private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    // Пока оставляем пустым, чтобы не мешать таймеру
}

// Когда начинаем тянуть ползунок — ставим на паузу
private void TimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
{
    isDragging = true;
}

// Когда отпустили — перематываем видео и аудио (синхронно!)
private void TimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
{
    isDragging = false;
    ResultPreview.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
    
    // Если аудио загружено, синхронизируем и его (с учетом StartSlider)
    if (AudioPreview.Source != null)
    {
        AudioPreview.Position = TimeSpan.FromSeconds(StartSlider.Value + TimelineSlider.Value);
    }
}


private async void SelectFiles_Click(object sender, RoutedEventArgs e)
{
        try
        {
            // 1. Выбираем видео
            OpenFileDialog videoDialog = new OpenFileDialog { Filter = "Video files|*.mp4;*.avi;*.mov" };
              if (videoDialog.ShowDialog() == true)
        {
            videoPath = videoDialog.FileName;
            
            // --- НОВАЯ ЛОГИКА: ЗАГРУЗКА В ПРЕДПРОСМОТР ---
            ResultPreview.Source = new Uri(videoPath);
            ResultPreview.Play(); // Запускаем, чтобы пользователь увидел первый кадр
            ResultPreview.Pause(); // Сразу ставим на паузу
            // ----------------------------------------------
        }
            else return;

            // 2. Выбираем музыку
            OpenFileDialog audioDialog = new OpenFileDialog { Filter = "Audio files|*.mp3;*.wav;*.m4a" };
            if (audioDialog.ShowDialog() == true)
            {
                audioPath = audioDialog.FileName; // БЕЗ слова string в начале!
            }
            else return;

            StatusText.Text = "Анализ файлов...";

            // Получаем инфо
                    isLoadingFiles = true;
            var vInfo = await FFmpeg.GetMediaInfo(videoPath);
            var aInfo = await FFmpeg.GetMediaInfo(audioPath);
            videoDuration = vInfo.Duration.TotalSeconds;

            VideoDurationText.Text = videoDuration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            videoDuration = vInfo.Duration.TotalSeconds;
            audioDuration = aInfo.Duration.TotalSeconds;


StartSlider.Maximum = Math.Max(0, audioDuration - videoDuration); // Чтобы музыка не кончилась раньше видео

StartSlider.Value = 0;
  // ВЫКЛЮЧАЕМ РЕЖИМ ЗАГРУЗКИ
        isLoadingFiles = false; 
         StatusText.Text = "Готово к настройке.";
          StatusText.Text = "Готово к настройке.";
            // Настраиваем слайдеры
        

            // Визуально подгоняем полоски
            VideoBar.Width = 300; 
            AudioBar.Width = (audioDuration / videoDuration) * 300;
            
            StatusText.Text = "Файлы загружены. Настройте длину и жмите Склеить.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при выборе файлов: {ex.Message}");
        }
}
// Добавь эти события в XAML к обоим слайдерам: PreviewMouseDown="Sliders_PreviewMouseDown"



private void StartSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    // 1. Проверки: не загружаются ли файлы и создан ли плеер
    if (isLoadingFiles || StartSlider == null || AudioPreview == null) return;

  
    AudioPreview.Position = TimeSpan.FromSeconds(StartSlider.Value);
    AudioPreview.Play();
    StopAudioAfterDelay(1500);
}




  private async void MergeButton_Click(object sender, RoutedEventArgs e)
    {
         // Освобождаем плеер перед работой FFmpeg
    ResultPreview.Source = null; 
        if (string.IsNullOrEmpty(videoPath) || string.IsNullOrEmpty(audioPath))
        {
            MessageBox.Show("Сначала выберите видео и аудио!");
            return;
        }

        SaveFileDialog saveDialog = new SaveFileDialog { Filter = "MP4 Video|*.mp4", FileName = "result.mp4" };
        if (saveDialog.ShowDialog() != true) return;
        string outputPath = saveDialog.FileName;

        try 
        {
            StatusText.Text = "Идет склейка...";
            ProgBar.IsIndeterminate = true;

            var vInfo = await FFmpeg.GetMediaInfo(videoPath);
            var aInfo = await FFmpeg.GetMediaInfo(audioPath);

            // Безопасное получение потоков
            var vStream = vInfo.VideoStreams.FirstOrDefault();
            var aStream = aInfo.AudioStreams.FirstOrDefault();

            if (vStream == null || aStream == null)
            {
                throw new Exception("Не удалось найти видео или аудио поток в файлах.");
            }
// 1. Вычисляем длительность
double startTime = StartSlider.Value;
double duration =  StartSlider.Value;

// Форматируем время для FFmpeg (с точкой вместо запятой)
   string ss = StartSlider.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    // Длительность отрезка аудио теперь ВСЕГДА равна длительности видео
    string t = videoDuration.ToString(System.Globalization.CultureInfo.InvariantCulture);


// Если длительность 0, склейка не имеет смысла

       // 2. Создаем сложную конвертацию
   var conversion = FFmpeg.Conversions.New()
            .AddParameter($"-i \"{videoPath}\"")
            .AddParameter($"-ss {ss} -t {t} -i \"{audioPath}\"")
            .AddParameter("-map 0:v:0") 
            .AddParameter("-map 1:a:0") 
            .AddParameter("-c:v copy")  
            .AddParameter("-c:a aac")   
            .AddParameter("-shortest")  
            .SetOutput(outputPath);

            await conversion.Start();
AudioPreview.Source = null; // Отключаем временный звук
ResultPreview.IsMuted = false; // Включаем звук у готового видео
ResultPreview.Source = new Uri(outputPath);
// Останавливаем старое видео, если оно играло
ResultPreview.Stop();
//ResultPreview.Source = null;

// Загружаем новый файл (нужно использовать Uri)
ResultPreview.Source = new Uri(outputPath);

// Автоматически запускаем просмотр
ResultPreview.Play();
StatusText.Text = "Склейка завершена! Видео воспроизводится.";



            ProgBar.IsIndeterminate = false;
            StatusText.Text = "Готово!";
            MessageBox.Show("Видео успешно сохранено!");
        }
        catch (Exception ex)
        {
            ProgBar.IsIndeterminate = false;
            StatusText.Text = "Ошибка склейки";
            MessageBox.Show($"Ошибка: {ex.Message}");
        }
    }
// 1. Метод для кнопки PLAY
private void PlayPreview_Click(object sender, RoutedEventArgs e)
{
     if (!string.IsNullOrEmpty(videoPath) && !string.IsNullOrEmpty(audioPath))
    {
        // 1. Убеждаемся, что источники на месте
        if (ResultPreview.Source == null) ResultPreview.Source = new Uri(videoPath);
        if (AudioPreview.Source == null) AudioPreview.Source = new Uri(audioPath);

        // 2. СБРОС (Важно! Это лечит отсутствие звука при первом нажатии)
        ResultPreview.Stop();
        AudioPreview.Stop();

        ResultPreview.IsMuted = true;
        
        // 3. Установка позиций
        ResultPreview.Position = TimeSpan.Zero;
        AudioPreview.Position = TimeSpan.FromSeconds(StartSlider.Value);

        // 4. Запуск с микро-паузой (необязательно, но надежно)
        ResultPreview.Play();
        AudioPreview.Play();
        
        timer.Start();
        StatusText.Text = "Играет предпросмотр...";
    }
    else
    {
        MessageBox.Show("Сначала выберите файлы!");
    }
     timer.Start();
}


// 2. Метод для кнопки PAUSE
private void PausePreview_Click(object sender, RoutedEventArgs e)
{
    ResultPreview.Pause();
    AudioPreview.Pause();
      timer.Stop();
}


// Клик по видео ставит его на паузу или запускает
private void ResultPreview_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    // Простая логика: если играет — пауза, если нет — старт
    // (Для этого можно добавить флаг IsPlaying, но для начала хватит и кнопок)
}
private async void StopAudioAfterDelay(int ms)
{
    await System.Threading.Tasks.Task.Delay(ms);
    if (AudioPreview != null) 
    {
        AudioPreview.Pause();
    }
}

}
