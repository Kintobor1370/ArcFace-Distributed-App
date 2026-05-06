using System.Collections.ObjectModel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using NuGet_ArcFace_Functions;

using Database;


namespace ServerClasses
{
    //____________________________________________________FUNCTIONS EXECUTED BY THE SERVER____________________________________________________
    public class ServerFunctions : MarshalByRefObject
    {
        private Functions ArcFace_Functions;        // Object of the class with functions for distance and similarity calculations
        private SemaphoreSlim Sem;

        public ServerFunctions()
        {
            this.ArcFace_Functions = new Functions("..\\arcfaceresnet100-8.onnx");
            this.Sem = new SemaphoreSlim(1);
        }

        public ObservableCollection<Database.Image> MakeCollection()
        {
            using (var db = new Database.Context())
            { 
                return new ObservableCollection<Database.Image>(db.Images);
            }
        }

        //.........................Resize the image to fit the required size before starting calculations
        private Image<Rgb24> GetValidSizeImage(Image<Rgb24> source_image)
        {
            Image<Rgb24> valid_size_image = source_image;
            var source_height = source_image.Height;
            var source_width = source_image.Width;

            if (source_height != source_width)
            {
                var target_size = Math.Min(source_height, source_width);
                valid_size_image = source_image.Clone(img =>
                    img.Resize(source_width, source_height).Crop(new Rectangle((source_width - target_size) / 2, 0, target_size, target_size)));
            }
            valid_size_image.Mutate(img => img.Resize(112, 112));
            return valid_size_image;
        }

        //.........................Get the Embedding vector of the image and add it to the storage (if there is no such image in the storage yet)
        public async Task<int> GetEmbedding(string image_name, byte[] image_data)
        {
            try
            {
                Task<float[]> embedding_task;
                Database.Image image_from_storage = null;
                await Sem.WaitAsync();

                // Check if the image is already in the storage (by hash-code)
                using (var db = new Database.Context())
                {
                    string image_hash = Database.Image.GetHash(image_data);
                    var q = db.Images.Where(x => x.Hash == image_hash);
                    if (q.Any())
                    {
                        image_from_storage = q.First();
                    }
                }
                Sem.Release();

                // If the image is not in the storage:
                if (image_from_storage == null)
                {
                    // Calculate the Embedding vector of the image
                    var valid_size_face = GetValidSizeImage(SixLabors.ImageSharp.Image.Load<Rgb24>(image_data));
                    embedding_task = ArcFace_Functions.CreateEmbedding(valid_size_face);
                    await Sem.WaitAsync();

                    // Add the image to the storage
                    using (var db = new Database.Context())
                    {
                        string image_hash = Database.Image.GetHash(image_data);
                        db.Add(
                            new Database.Image
                            {
                                Name = image_name,
                                Data = image_data,
                                Hash = image_hash,
                                Embedding = Converter.FloatToByte(embedding_task.Result)
                            }
                        );
                        db.SaveChanges();
                        image_from_storage = db.Images.Where(x => x.Hash == image_hash).First();
                    }
                    Sem.Release();
                }
                return image_from_storage.ID;
            }
            catch(Exception ex)
            { 
                return -1; 
            }
        }

        //.........................Get the array of identifiers of all images in the storage
        public async Task<int[]> GetAllImages()
        {
            int[] ImageIDs = new int[] {1, 2};

            await Sem.WaitAsync();
            using (var db = new Database.Context())
            {
                var images = new ObservableCollection<Database.Image>(db.Images);
                ImageIDs = new int[images.Count];
                for (int i = 0; i < images.Count; i++)
                    ImageIDs[i] = images[i].ID;
                images.Clear();
            }
            Sem.Release();
            return ImageIDs;
        }

        //.........................Get the image by its identifier in the storage
        public async Task<Database.Image> GetImageByID(int id)
        {
            try
            {
                Database.Image found_image;

                await Sem.WaitAsync();
                using (var db = new Database.Context())
                {
                    var q = db.Images.Where(x => x.ID == id);
                    if (q.Any())
                    {
                        found_image = q.First();
                    }
                    else
                    {
                        found_image = null;
                    }
                }
                Sem.Release();
                return found_image;
            }
            catch (Exception ex)
            {
                return null; 
            }
        }

        //.........................Delete all images from the storage
        public async Task<int> DeleteImages(CancellationToken token)
        {
            try
            {
                int res = 1;

                await Sem.WaitAsync();
                using (var db = new Database.Context())
                {
                    var buffer = new ObservableCollection<Database.Image>(db.Images);
                    db.Images.RemoveRange(db.Images);
                    db.SaveChanges();

                    if (token.IsCancellationRequested)
                    {
                        for (int i = 0; i < buffer.Count; i++)
                        {
                            db.Add(buffer[i]);
                        }
                        db.SaveChanges();
                        res = 0;
                    }
                }
                Sem.Release();
                return res;
            }
            catch (Exception ex)
            { 
                return -1; 
            }
        }
    }

    //_________________________________________________FUNCTIONS EXECUTED BY THE CONTROLLER___________________________________________________
    public class ControllerFunctions : ServerFunctions
    {
        //.........................POST: Calculate the Embedding vector of the image and add it to the storage
        public async Task<(bool, int)> PostImage(PostData data, CancellationToken token)
        {
            var ID = await GetEmbedding(data.Name, data.Data);
            if (token.IsCancellationRequested)
            {
                return (false, -1);
            }
            return (true, ID);
        }

        //.........................GET: the array of identifiers of all images in the storage
        public async Task<(bool, int[]?)> GetAllImages(CancellationToken token)
        {
            var ImageIDs = await GetAllImages();
            if (token.IsCancellationRequested)
            {
                return (false, null);
            }
            return (true, ImageIDs);
        }

        //.........................GET: the image by its identifier in the storage
        public async Task<(bool, Database.Image?)> TryGetImageByID(int id, CancellationToken token)
        {
            var FoundImageID = await GetImageByID(id);
            if (token.IsCancellationRequested)
            {
                return (false, null);
            }
            return (true, FoundImageID);
        }

        //.........................DELETE: all images from the storage
        public async Task<int> DeleteAllImages(CancellationToken token)
        { 
            return await DeleteImages(token); 
        }
    }
}
