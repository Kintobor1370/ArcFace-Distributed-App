using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Net.Http.Json;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System.Diagnostics;

using NuGet_ArcFace_Functions;
using Database;


namespace WpfApp1
{
    //______________________________LISTBOX ITEM (ABSOLUTE PATH TO THE IMAGE FILE, FILE NAME AND FILE EXTENSION)______________________________
    public class ListItem
    {
        private string path;
        private string name;
        private string ext;
        public string Path { get { return path; } }
        public string Name { get { return name; } }
        public string Ext { get { return ext; } }

        public ListItem(string file_path)
        {
            path = file_path;
            name = System.IO.Path.GetFileName(file_path);
            ext = System.IO.Path.GetExtension(file_path);
        }
    }

    //_______________________________________________________WPF APPLICATION CLASSES__________________________________________________________
    public class ViewModel : INotifyPropertyChanged
    {
        private string folder_path;
        private string img1;
        private string img2;
        private float distance;
        private float similarity;
        private bool changed;
        private bool cancellable;
        private bool not_empty;

        //.........................Absolute path to the image folder (for output in TextBox)
        public string FolderPath
        {
            get
            { 
                return folder_path; 
            }
            set
            { 
                folder_path = value; 
                OnPropertyChanged("FolderPath"); 
            }
        }

        //.........................Absolute path to the first image
        public string Image1
        {
            get 
            { 
                return img1; 
            }
            set 
            { 
                img1 = value; 
                OnPropertyChanged("Image1"); 
            }
        }

        //.........................Absolute path to the second image
        public string Image2
        {
            get 
            { 
                return img2; 
            }
            set 
            { 
                img2 = value; 
                OnPropertyChanged("Image2"); 
            }
        }

        //.........................Distance between the two selected images
        public float Distance
        {
            get 
            { 
                return distance; 
            }
            set 
            { 
                distance = value; 
                OnPropertyChanged("Distance"); 
            }
        }

        //.........................Similarity rate between the two selected images
        public float Similarity
        {
            get 
            { 
                return similarity; 
            }
            set 
            { 
                similarity = value; 
                OnPropertyChanged("Similarity"); 
            }
        }

        //.........................Ability to start calculations (disabled when calculations for two selected images are already completed)
        public bool ImagesChanged
        {
            get 
            { 
                return changed; 
            }
            set 
            { 
                changed = value; 
                OnPropertyChanged("ImagesChanged"); 
            }
        }

        //.........................Ability to cancel calculations (disabled when there are no active calculations)
        public bool Cancellable
        {
            get 
            { 
                return cancellable; 
            }
            set 
            { 
                cancellable = value; 
                OnPropertyChanged("Cancellable"); 
            }
        }

        //.........................Indicator of the presence of images in the persistent storage (if there are no images, the delete button will be disabled)
        public bool StorageNotEmpty
        {
            get 
            { 
                return not_empty; 
            }
            set 
            { 
                not_empty = value; 
                OnPropertyChanged("StorageNotEmpty"); 
            }
        }

        //.........................Collection of the names of images from the selected folder and their absolute paths (used for output through ListBox)
        public ObservableCollection<ListItem> Files { get; set; }

        //.........................Collection of the images from the persistent storage (used for output through ListBox)
        public ObservableCollection<Database.Image> Images { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }
        void FilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        { 
            OnPropertyChanged("Files");
        }
        void ImagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        { 
            OnPropertyChanged("Images"); 
        }

        public ViewModel()
        {
            FolderPath = "Path to the images folder will be displayed here";
            Image1 = null;
            Image2 = null;

            Files = new ObservableCollection<ListItem>();
            Images = new ObservableCollection<Database.Image>();
            Files.CollectionChanged += FilesCollectionChanged;
            Images.CollectionChanged += ImagesCollectionChanged;

            ImagesChanged = true;
            Cancellable = false;
        }
    }

    //_____________________________________________________MAIN WINDOW OF THE APPLICATION_____________________________________________________
    public partial class MainWindow : Window
    {
        private readonly bool modelRequired = false;
        private readonly Uri ServerLink;
        private readonly AsyncRetryPolicy policy;

        private bool cancellation = false;
        private CancellationTokenSource source;
        private string DistanceTokenKey;                // Cancellation token for distance calculation (used for cancellation of the corresponding Task in ArcFace_Functions)
        private string SimilarityTokenKey;              // Cancellation token for similarity calculation (used for cancellation of the corresponding Task in ArcFace_Functions)

        public Functions ArcFace_Functions;
        public ViewModel ViewModel;
        
        public MainWindow()
        {
            ServerLink = new Uri("http://localhost:5240/images");
            source = new CancellationTokenSource();
            ArcFace_Functions = new Functions("..\\..\\..\\..\\arcfaceresnet100-8.onnx");
            ViewModel = new ViewModel();

            policy = Policy.Handle<HttpRequestException>().WaitAndRetryAsync(
                3,
                times => TimeSpan.FromMilliseconds(times*10000)
            );
            InitializeComponent();
            this.DataContext = ViewModel;

            Image1ListBox.SelectionChanged += Image1SelectionChanged;
            Image2ListBox.SelectionChanged += Image2SelectionChanged;

            UpdateImages("");
        }

        //.........................Update the observable collection of the persistent storage items
        private async void UpdateImages(string final_text)
        {
            try
            {
                ImagesListBoxStatus.Text = "Loading images...";
                var ImagesFromServer = new ObservableCollection<Database.Image>();

                ServerConnection.Text = "Establishing connection to the server...";
                await policy.ExecuteAsync(async () =>
                {
                    var Client = new HttpClient();
                    var response = await Client.GetAsync(ServerLink, source.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        // Get the array of the images identifiers in the storage
                        var ids_string = response.Content.ReadAsStringAsync().Result;
                        var ids = JsonConvert.DeserializeObject<int[]>(ids_string);

                        // If there are no images in the storage (initial state of the database)
                        if (ids.Length == 0)
                        {
                            ViewModel.Images.Clear();
                            ViewModel.StorageNotEmpty = false;
                        }
                        // If there are images in the storage
                        else
                        {
                            foreach (var id in ids)
                            {
                                // Get each image from the storage by its identifier
                                string ID_Link = ServerLink + "/id?id=" + id.ToString();
                                var img_response = await Client.GetAsync(ID_Link, source.Token);

                                var image_string = img_response.Content.ReadAsStringAsync().Result;
                                var image = JsonConvert.DeserializeObject<Database.Image>(image_string);

                                // Add it to the observable collection of the images from the storage
                                ImagesFromServer.Add(image);
                            }
                            ViewModel.Images.Clear();
                            foreach (var image in ImagesFromServer)
                            {
                                ViewModel.Images.Add(image);
                            }
                            ViewModel.StorageNotEmpty = true;
                        }
                        ServerConnection.Text = "Connection established!";
                        ImagesListBoxStatus.Text = final_text;
                    }
                    else
                    { 
                        ImagesListBoxStatus.Text = "Error in loading images!"; 
                    }
                });
            }
            catch(HttpRequestException)
            { 
                System.Windows.MessageBox.Show(
                    "Failed to connect to the server:\n" + ServerLink, 
                    "Http Request Exception", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error
                ); 
            }
        }

        //.........................Indicators of ListBox items selection
        private void Image1SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.ImagesChanged = true;
            int Index = Image1ListBox.SelectedIndex;
            if (Index != -1)
            {
                ViewModel.Image1 = ViewModel.Files[Index].Path;
            }
        }
        private void Image2SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.ImagesChanged = true;
            int Index = Image2ListBox.SelectedIndex;
            if (Index != -1)
            {
                ViewModel.Image2 = ViewModel.Files[Index].Path;
            }
        }

        //.........................Find and open the image folder
        private void Browse_Folders_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog open_folder = new FolderBrowserDialog();
            if (open_folder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ViewModel.Image1 = null;
                ViewModel.Image2 = null;

                ViewModel.FolderPath = open_folder.SelectedPath;
                ShowFolderContents();
            }
        }

        //.........................Add images in JPG and PNG formats from the selected folder to the corresponding collections
        private void ShowFolderContents()
        {
            ViewModel.Files.Clear();
            string[] filepaths = Directory.GetFiles(ViewModel.FolderPath);
            foreach (string filepath in filepaths)
            {
                var listItem = new ListItem(filepath);
                if (listItem.Ext == ".jpg" || listItem.Ext == ".png")
                {
                    ViewModel.Files.Add(listItem);
                }
            }
        }

        //.........................Request to the server: POST
        private async Task<int> PostImage(string image_path)
        {
            var img = System.Drawing.Image.FromFile(image_path);
            var ms = new MemoryStream();
            img.Save(ms, img.RawFormat);
            byte[] image_data = ms.ToArray();

            try
            {
                ServerConnection.Text = "Establishing connection to the server...";
                var image = new PostData
                {
                    Name = System.IO.Path.GetFileName(image_path),
                    Data = image_data
                };

                return await policy.ExecuteAsync(async () =>
                {
                    ServerConnection.Text = "Connection established!";

                    var Client = new HttpClient();
                    Client.BaseAddress = ServerLink;
                    Client.DefaultRequestHeaders.Accept.Clear();
                    Client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    var response = await HttpClientJsonExtensions.PostAsJsonAsync(Client, "images", image, source.Token);
                    var res = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine(res);
                    var id = await response.Content.ReadFromJsonAsync<int>();
                    return id;
                });
            }
            catch (HttpRequestException)
            {
                System.Windows.MessageBox.Show(
                    "Failed to connect to the server:\n" + ServerLink, 
                    "Http Request Exception", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error
                );
                return -1;
            }
        }

        //.........................Request to the server: GET /images/id
        private async Task<Task<float[]>> GetEmbeddingTask(int id)
        {
            string ID_Link = ServerLink + "/id?id=" + id.ToString();
            try
            {
                ServerConnection.Text = "Establishing connection to the server...";
                return await policy.ExecuteAsync(async () =>
                {
                    ServerConnection.Text = "Connection established!";

                    var Client = new HttpClient();
                    var response = await Client.GetAsync(ID_Link);
                    var json_string = await response.Content.ReadAsStringAsync();
                    var image = JsonConvert.DeserializeObject<Database.Image>(json_string);

                    Func<float[]> embedding = () => { return Converter.ByteToFloat(image.Embedding); };
                    var embedding_task = new Task<float[]>(embedding, TaskCreationOptions.LongRunning);
                    embedding_task = Task<float[]>.Run(embedding);

                    return embedding_task;
                });
            }
            catch (HttpRequestException)
            {
                System.Windows.MessageBox.Show(
                    "Failed to connect to the server:\n" + ID_Link, 
                    "Http Request Exception", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error
                );
                return null;
            }
        }

        //.........................Request to the server: DELETE /images
        private async void DeleteAllImages()
        {
            try
            {
                ServerConnection.Text = "Establishing connection to the server...";
                await policy.ExecuteAsync(async () =>
                {
                    ServerConnection.Text = "Connection established!";

                    var Client = new HttpClient();
                    var response = await Client.DeleteAsync(ServerLink, source.Token);
                    var result = await response.Content.ReadAsStringAsync();
                });
            }
            catch (HttpRequestException)
            { 
                System.Windows.MessageBox.Show(
                    "Failed to connect to the server:\n" + ServerLink, 
                    "Http Request Exception", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error
                ); 
            }
        }

        //.........................Check that both images are selected and that the images are loaded from the storage
        private bool CheckSelectedImages()
        {
            if (ViewModel.Files.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "Please select images folder",
                    "Images folder not slected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }
            else if (ViewModel.Image1 == null && ViewModel.Image2 == null)
            {
                System.Windows.MessageBox.Show(
                    "Please select one image from every list",
                    "Images not selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }
            else if (ViewModel.Image1 == null)
            {
                System.Windows.MessageBox.Show(
                    "Please select the first image",
                    "First image not selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }
            else if (ViewModel.Image2 == null)
            {
                System.Windows.MessageBox.Show(
                    "Please select the second image",
                    "Second image not selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }
            else if (ImagesListBoxStatus.Text == "Loading images...")
            {
                System.Windows.MessageBox.Show(
                    "Loading images from the storage, please wait",
                    "Loading images",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }
            else
            {
                return true;
            }
        }

        //.........................Calculate distance and similarity rate for the selected images
        private async void Start_Calculations_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Distance = 0;
            ViewModel.Similarity = 0;
            DistanceTokenKey = Guid.NewGuid().ToString();
            SimilarityTokenKey = Guid.NewGuid().ToString();
            pbStatus.Value = 0;                                         // ProgressBar для to display the progress of calculations

            if (ViewModel.ImagesChanged && CheckSelectedImages())
            {
                ViewModel.ImagesChanged = false;
                ViewModel.Cancellable = true;
                pbStatus.Value += 10;

                // Add the selected images to the persistent storage (if they are absent)
                int ID1 = await PostImage(ViewModel.Image1);
                int ID2 = await PostImage(ViewModel.Image2);
                pbStatus.Value += 10;

                // Get the calculated Embedding vectors for the selected images from the storage and convert them to Task<float[]>
                var embedding_task_1 = await GetEmbeddingTask(ID1);
                var embedding_task_2 = await GetEmbeddingTask(ID2);
                if (cancellation)
                {
                    ViewModel.Distance = -1;
                    ViewModel.Similarity = -1;
                    ViewModel.ImagesChanged = true;
                    pbStatus.Value = 0;
                    ViewModel.Cancellable = false;
                    System.Windows.MessageBox.Show("All calculations cancelled!");
                    return;
                }
                pbStatus.Value += 10;

                // Start asynchronous calculations of distance and similarity rate
                var distance = ArcFace_Functions.GetDistanceAsync(embedding_task_1, embedding_task_2, DistanceTokenKey);
                var similarity = ArcFace_Functions.GetSimilarityAsync(embedding_task_1, embedding_task_2, SimilarityTokenKey);
                pbStatus.Value += 10;

                var ActiveTasks = new List<Task> { distance, similarity };
                while (ActiveTasks.Count > 0)
                {
                    Task finished = await Task.WhenAny(ActiveTasks);

                    if (finished.Status == TaskStatus.Canceled)
                    {
                        ActiveTasks.Clear();
                        ViewModel.Distance = -1;
                        ViewModel.Similarity = -1;
                        ViewModel.ImagesChanged = true;
                        pbStatus.Value = 0;
                        System.Windows.MessageBox.Show("All calculations cancelled!");
                    }
                    else
                    {
                        if (finished == distance)
                        {
                            ViewModel.Distance = distance.Result;
                            pbStatus.Value += 30;
                        }
                        else if (finished == similarity)
                        {
                            ViewModel.Similarity = similarity.Result;
                            pbStatus.Value += 30;
                        }
                        else
                        {
                            System.Windows.MessageBox.Show("How is this even possible?!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    ActiveTasks.Remove(finished);
                }
                ViewModel.Cancellable = false;
                UpdateImages("Loading complete!");
            }
        }

        //.........................Button for cancelling active calculations of distance and similarity rate for the selected images
        private async void Cancel_Calculations_Click(object sender, RoutedEventArgs e)
        {
            cancellation = true;
            ArcFace_Functions.Cancel(DistanceTokenKey);
            ArcFace_Functions.Cancel(SimilarityTokenKey);
        }

        //.........................Button for deleting all images from the persistent storage
        private async void Delete_Images_Click(object sender, RoutedEventArgs e)
        {
            DeleteAllImages();
            ViewModel.Images.Clear();
            ViewModel.StorageNotEmpty = false;
            ImagesListBoxStatus.Text = "All images deleted!";
        }
    }

    //______________________________________________________________CONVERTERS________________________________________________________________
    public class DistanceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                float val = (float) value;
                string distance = val == -1 ? "Cancelled" : val.ToString();
                string result = "Distance:  " + distance;
                return result;
            }
            catch
            { 
                return "EX"; 
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        { 
            throw new NotImplementedException(); 
        }
    }

    public class SimilarityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                float val = (float) value;
                string similarity = val == -1 ? "Cancelled" : val.ToString();
                string result = "Similarity: " + similarity;
                return result;
            }
            catch
            { 
                return "EX"; 
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        { 
            throw new NotImplementedException(); 
        }
    }

    public class ImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                var ByteArray = value as byte[];
                if (ByteArray == null)
                {
                    return null;
                }
                System.Drawing.Image img;
                using (var ms = new MemoryStream(ByteArray))
                { 
                    img = System.Drawing.Image.FromStream(ms); 
                }
                return img;
            }
            catch
            { 
                return "EX"; 
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
