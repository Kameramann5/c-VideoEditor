using System.Windows;
using Microsoft.Win32; // Для OpenFileDialog
using Xabe.FFmpeg;
using System.Windows.Input;

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
        trimTimer.Interval = TimeSpan.FromMilliseconds(100);
        trimTimer.Tick += TrimTimer_Tick;

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



// Добавь эти события в XAML к обоим слайдерам: PreviewMouseDown="Sliders_PreviewMouseDown"
private async void Grid_MouseDown(object sender, MouseButtonEventArgs e)
{
    // Открываем диалог выбора файлов
    OpenFileDialog dialog = new OpenFileDialog 
    { 
        Filter = "Все медиафайлы|*.mp4;*.avi;*.mov;*.mp3;*.wav;*.m4a|Видео|*.mp4;*.avi;*.mov|Аудио|*.mp3;*.wav;*.m4a",
        Title = "Выберите файл для загрузки"
    };

    if (dialog.ShowDialog() == true)
    {
        string file = dialog.FileName;
        string ext = System.IO.Path.GetExtension(file).ToLower();

        // Используем ту же логику распределения, что и в General_Drop
        if (ext == ".mp4" || ext == ".avi" || ext == ".mov")
        {
            await ProcessVideo(file);
        }
        else if (ext == ".mp3" || ext == ".wav" || ext == ".m4a")
        {
            await ProcessAudio(file);
        }
    }
}

private async void SelectVideo_Click(object sender, RoutedEventArgs e)
{
    OpenFileDialog videoDialog = new OpenFileDialog { Filter = "Video files|*.mp4;*.avi;*.mov" };
    if (videoDialog.ShowDialog() == true)
    {
        try 
        {
            videoPath = videoDialog.FileName;
            ResultPreview.Source = new Uri(videoPath);
            ResultPreview.Play();
            ResultPreview.Pause();

            var vInfo = await FFmpeg.GetMediaInfo(videoPath);
            videoDuration = vInfo.Duration.TotalSeconds;
            VideoDurationText.Text = videoDuration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            StatusText.Text = "Видео загружено. Выберите аудио.";
            TryUpdateAnalysis(); // Проверяем, можно ли считать общую логику
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка видео: {ex.Message}"); }
    }
}

private async void SelectAudio_Click(object sender, RoutedEventArgs e)
{
    OpenFileDialog audioDialog = new OpenFileDialog { Filter = "Audio files|*.mp3;*.wav;*.m4a" };
    if (audioDialog.ShowDialog() == true)
    {
        try 
        {
            audioPath = audioDialog.FileName;
            var aInfo = await FFmpeg.GetMediaInfo(audioPath);
            audioDuration = aInfo.Duration.TotalSeconds;

            StatusText.Text = "Аудио загружено.";
            TryUpdateAnalysis();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка аудио: {ex.Message}"); }
    }
}

private void TryUpdateAnalysis()
{
    // 1. Обновляем аудио-полоску всегда, если аудио выбрано (даже без видео)
    if (!string.IsNullOrEmpty(audioPath) && audioDuration > 0)
    {
        // Если видео уже есть, рисуем пропорционально, если нет — просто фиксированную длину
        if (videoDuration > 0)
        {
            AudioBarOne.Width = (audioDuration / videoDuration) * 300;
        }
        else
        {
            AudioBarOne.Width = 300; // Просто показываем, что файл принят
        }
    }

    // 2. Если оба пути установлены — настраиваем общую логику
    if (!string.IsNullOrEmpty(videoPath) && !string.IsNullOrEmpty(audioPath))
    {
        isLoadingFiles = true;

        // Настройка слайдера
        StartSlider.Maximum = Math.Max(0, audioDuration - videoDuration);
        StartSlider.Value = 0;

        // Визуальное отображение полосок
        VideoBarOne.Width = 300; 
        // Здесь ширина аудио пересчитается точно по пропорции к видео
        AudioBarOne.Width = (audioDuration / videoDuration) * 300;

        isLoadingFiles = false;
        StatusText.Text = "Файлы готовы. Настройте смещение и жмите Склеить.";
    }
}



private void StartSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    // 1. Проверки: не загружаются ли файлы и создан ли плеер
    if (isLoadingFiles || StartSlider == null || AudioPreview == null) return;

  
    AudioPreview.Position = TimeSpan.FromSeconds(StartSlider.Value);
    AudioPreview.Play();
    StopAudioAfterDelay(1500);
}

// 1. Общий обработчик для перетаскивания на всю область Grid
private async void General_Drop(object sender, DragEventArgs e)
{
    if (e.Data.GetDataPresent(DataFormats.FileDrop))
    {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length > 0)
        {
            string file = files[0];
            string ext = System.IO.Path.GetExtension(file).ToLower();

            // Проверяем расширение и вызываем нужный метод
            if (ext == ".mp4" || ext == ".avi" || ext == ".mov")
                await ProcessVideo(file);
            else if (ext == ".mp3" || ext == ".wav" || ext == ".m4a")
                await ProcessAudio(file);
        }
    }
}

// 2. Метод для визуального эффекта (курсор "плюсик")
private void Element_DragOver(object sender, DragEventArgs e)
{
    e.Effects = DragDropEffects.Copy;
    e.Handled = true;
}

// 3. Выносим логику обработки видео (чтобы вызывалась и из кнопки, и из Drop)
private async Task ProcessVideo(string path)
{
    try {
        videoPath = path;
        ResultPreview.Source = new Uri(videoPath);
        ResultPreview.Play(); ResultPreview.Pause();

        var vInfo = await FFmpeg.GetMediaInfo(videoPath);
        videoDuration = vInfo.Duration.TotalSeconds;
        VideoDurationText.Text = videoDuration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        
        VideoBarOne.Width = 300; // Показываем полоску
        StatusText.Text = "Видео загружено.";
        TryUpdateAnalysis();
    } catch (Exception ex) { MessageBox.Show(ex.Message); }
}

// 4. Выносим логику обработки аудио
private async Task ProcessAudio(string path)
{
    try {
        audioPath = path;
        var aInfo = await FFmpeg.GetMediaInfo(audioPath);
        audioDuration = aInfo.Duration.TotalSeconds;
        
        StatusText.Text = "Аудио загружено.";
        TryUpdateAnalysis();
    } catch (Exception ex) { MessageBox.Show(ex.Message); }
}



private async void VideoBar_Drop(object sender, DragEventArgs e)
{
    if (e.Data.GetDataPresent(DataFormats.FileDrop))
    {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length > 0) 
        {
            await ProcessVideo(files[0]); // Вызываем общий метод обработки
        }
    }
}

private async void AudioBar_Drop(object sender, DragEventArgs e)
{
    if (e.Data.GetDataPresent(DataFormats.FileDrop))
    {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length > 0)
        {
            await ProcessAudio(files[0]); // Вызываем общий метод обработки
        }
    }
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

// ============ ВКЛАДКА "ОБРЕЗАТЬ ВИДЕО" ============

string trimVideoPath = "";
double trimVideoDuration = 0;
System.Windows.Threading.DispatcherTimer trimTimer = new();
bool trimIsDragging = false;
bool trimIsPlaying = false;

private void TrimTimelineSlider_SizeChanged(object sender, SizeChangedEventArgs e)
{
    UpdateTrimRangeIndicator();
}

private void UpdateTrimRangeIndicator()
{
    if (TrimTimelineSlider == null || TrimRangeIndicator == null) return;
    double w = TrimTimelineSlider.ActualWidth;
    if (w <= 0 || trimVideoDuration <= 0) return;

    double start = TrimStartSlider.Value;
    double end = TrimEndSlider.Value;
    double left = (start / trimVideoDuration) * w;
    double width = ((end - start) / trimVideoDuration) * w;

    TrimRangeIndicator.Margin = new Thickness(left, 0, 0, 0);
    TrimRangeIndicator.Width = Math.Max(0, width);
}

private void TrimTimer_Tick(object? sender, EventArgs e)
{
    if (TrimPreview.Source != null && TrimPreview.NaturalDuration.HasTimeSpan && !trimIsDragging)
    {
        double max = TrimPreview.NaturalDuration.TimeSpan.TotalSeconds;
        TrimTimelineSlider.Maximum = max;
        TrimTimelineSlider.Value = TrimPreview.Position.TotalSeconds;

        TrimCurrentTimeText.Text = TrimPreview.Position.ToString(@"mm\:ss");
        TrimTotalTimeText.Text = TrimPreview.NaturalDuration.TimeSpan.ToString(@"mm\:ss");
    }
}

private async void TrimVideo_Drop(object sender, DragEventArgs e)
{
    if (e.Data.GetDataPresent(DataFormats.FileDrop))
    {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length > 0)
        {
            string file = files[0];
            string ext = System.IO.Path.GetExtension(file).ToLower();
            if (ext == ".mp4" || ext == ".avi" || ext == ".mov" || ext == ".mkv" || ext == ".wmv")
            {
                await TrimLoadVideo(file);
            }
        }
    }
}

private async void TrimGrid_MouseDown(object sender, MouseButtonEventArgs e)
{
    OpenFileDialog dialog = new OpenFileDialog
    {
        Filter = "Видео|*.mp4;*.avi;*.mov;*.mkv;*.wmv",
        Title = "Выберите видео для обрезки"
    };

    if (dialog.ShowDialog() == true)
    {
        await TrimLoadVideo(dialog.FileName);
    }
}

private async Task TrimLoadVideo(string path)
{
    try
    {
        TrimPreview.Source = null;
        trimVideoPath = path;
        TrimPreview.Source = new Uri(trimVideoPath);
        TrimPreview.Play();
        TrimPreview.Pause();

        var vInfo = await FFmpeg.GetMediaInfo(trimVideoPath);
        trimVideoDuration = vInfo.Duration.TotalSeconds;
        TrimVideoDurationText.Text = trimVideoDuration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        TrimStartSlider.Maximum = trimVideoDuration;
        TrimEndSlider.Maximum = trimVideoDuration;
        TrimStartSlider.Value = 0;
        TrimEndSlider.Value = trimVideoDuration;
        TrimTimelineSlider.Maximum = trimVideoDuration;

        UpdateTrimDuration();
        UpdateTrimRangeIndicator();

        TrimStatusText.Text = "Видео загружено. Настройте отрезок и сохраните.";
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Ошибка загрузки видео: {ex.Message}");
    }
}

private void UpdateTrimDuration()
{
    double duration = Math.Max(0, TrimEndSlider.Value - TrimStartSlider.Value);
    TrimDurationText.Text = duration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
}

private void TrimStartSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    if (TrimStartSlider == null || TrimEndSlider == null) return;

    if (TrimStartSlider.Value >= TrimEndSlider.Value)
    {
        TrimStartSlider.Value = Math.Max(0, TrimEndSlider.Value - 1);
    }

    UpdateTrimDuration();
    UpdateTrimRangeIndicator();

    if (TrimPreview.Source != null && TrimPreview.NaturalDuration.HasTimeSpan)
    {
        TrimPreview.Position = TimeSpan.FromSeconds(TrimStartSlider.Value);
    }
}

private void TrimEndSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    if (TrimStartSlider == null || TrimEndSlider == null) return;

    if (TrimEndSlider.Value <= TrimStartSlider.Value)
    {
        TrimEndSlider.Value = Math.Min(trimVideoDuration, TrimStartSlider.Value + 1);
    }

    UpdateTrimDuration();
    UpdateTrimRangeIndicator();

    if (TrimPreview.Source != null && TrimPreview.NaturalDuration.HasTimeSpan)
    {
        TrimPreview.Position = TimeSpan.FromSeconds(TrimEndSlider.Value);
    }
}

private void TrimTimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
{
    trimIsDragging = true;
}

private void TrimTimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
{
    trimIsDragging = false;
    if (TrimPreview.Source != null)
    {
        TrimPreview.Position = TimeSpan.FromSeconds(TrimTimelineSlider.Value);
    }
}

private void TrimPlay_Click(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrEmpty(trimVideoPath)) return;

    if (TrimPreview.Source == null)
        TrimPreview.Source = new Uri(trimVideoPath);

    if (TrimPreview.IsMuted)
        TrimPreview.IsMuted = false;

    if (trimIsPlaying)
    {
        TrimPreview.Pause();
        trimTimer.Stop();
        trimIsPlaying = false;
        TrimStatusText.Text = "Пауза";
        return;
    }

    double pos = TrimPreview.Position.TotalSeconds;

    if (pos >= TrimEndSlider.Value || pos < TrimStartSlider.Value)
    {
        TrimPreview.Position = TimeSpan.FromSeconds(TrimStartSlider.Value);
    }

    TrimPreview.Play();
    trimTimer.Start();
    trimIsPlaying = true;
    TrimStatusText.Text = "Воспроизведение отрезка...";
}

private void TrimRewind_Click(object sender, RoutedEventArgs e)
{
    if (TrimPreview.Source == null) return;
    TrimPreview.Pause();
    trimTimer.Stop();
    trimIsPlaying = false;
    TrimPreview.Position = TimeSpan.FromSeconds(0);
    TrimTimelineSlider.Value = 0;
    TrimStatusText.Text = "В начало";
}

private async void TrimSaveButton_Click(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrEmpty(trimVideoPath))
    {
        MessageBox.Show("Сначала загрузите видео!");
        return;
    }

    double start = TrimStartSlider.Value;
    double end = TrimEndSlider.Value;

        TrimPreview.Pause();
    trimTimer.Stop();
    trimIsPlaying = false;

    SaveFileDialog saveDialog = new SaveFileDialog
    {
        Filter = "MP4 Video|*.mp4",
        FileName = "trimmed_video.mp4"
    };

    if (saveDialog.ShowDialog() != true) return;
    string outputPath = saveDialog.FileName;

    try
    {
        TrimStatusText.Text = "Обрезка видео...";
        TrimProgBar.IsIndeterminate = true;

        string ss = start.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        string to = end.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        var conversion = FFmpeg.Conversions.New()
            .AddParameter($"-ss {ss}")
            .AddParameter($"-i \"{trimVideoPath}\"")
            .AddParameter($"-to {to}")
            .AddParameter("-c copy")
            .AddParameter("-avoid_negative_ts make_zero")
            .SetOutput(outputPath);

        await conversion.Start();

        TrimProgBar.IsIndeterminate = false;
        TrimStatusText.Text = "Готово!";
        MessageBox.Show($"Видео обрезано и сохранено:\n{outputPath}");

        TrimPreview.Source = null;
        trimVideoPath = outputPath;
        TrimPreview.Source = new Uri(trimVideoPath);
        TrimPreview.Play();
        TrimPreview.Pause();

        var vInfo = await FFmpeg.GetMediaInfo(trimVideoPath);
        trimVideoDuration = vInfo.Duration.TotalSeconds;
        TrimVideoDurationText.Text = trimVideoDuration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        TrimStartSlider.Maximum = trimVideoDuration;
        TrimEndSlider.Maximum = trimVideoDuration;
        TrimStartSlider.Value = 0;
        TrimEndSlider.Value = trimVideoDuration;
        TrimTimelineSlider.Maximum = trimVideoDuration;
        UpdateTrimDuration();
        UpdateTrimRangeIndicator();
    }
    catch (Exception ex)
    {
        TrimProgBar.IsIndeterminate = false;
        TrimStatusText.Text = "Ошибка обрезки";
        MessageBox.Show($"Ошибка: {ex.Message}");
    }
}

}
