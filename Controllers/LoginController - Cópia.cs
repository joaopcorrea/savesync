//using Google.Apis.Auth.OAuth2;
//using Google.Apis.Drive.v3;
//using Google.Apis.Services;
//using Google.Apis.Util.Store;
//using System.Globalization;
//using System.Security.Cryptography;

//public class LoginController
//{
//    static string[] Scopes = { DriveService.Scope.DriveFile };
//    static string ApplicationName = "Google Drive Upload";

//    public void Login(string email, string diretorio)
//    {
//        Console.Write("Informe seu e-mail do Google: ");
//        string userEmail = email;

//        UserCredential credential = AuthenticateUser(userEmail);
//        var service = new DriveService(new BaseClientService.Initializer()
//        {
//            HttpClientInitializer = credential,
//            ApplicationName = ApplicationName,
//        });

//        Console.Write("Informe o caminho do arquivo para upload: ");
//        string filePath = diretorio;


//        string rootFolderName = "savesync";
//        string subFolderName = "saves";
//        string localFolderPath = @"D:\savesync-saves"; // Caminho da pasta local

//        string rootFolderId = GetOrCreateFolder(service, rootFolderName);
//        string subFolderId = GetOrCreateFolder(service, subFolderName, rootFolderId);

//        // Verifica se a pasta "saves" existe no Google Drive, senão cria
//        //string folderId = GetOrCreateFolder(service, folderName);

//        // Sincroniza os saves entre o Google Drive e a máquina local
//        SyncSaves(service, subFolderId, localFolderPath);


//        //// Verifica se a pasta no Google Drive tem arquivos mais recentes ou mais arquivos
//        //CompareAndUpdateFolder(service, folderId, localFolderPath);

//        //// Verifica se um arquivo específico no Google Drive está mais recente que o local
//        //string fileName = "testao.txt";
//        ////string filePath = @"caminho\para\o\arquivo.txt"; // Caminho do arquivo local
//        //CheckAndUpdateFile(service, fileName, filePath);


//        //if (File.Exists(filePath))
//        //{
//        //    UploadFile(service, filePath);
//        //}
//        //else
//        //{
//        //    Console.WriteLine(" Arquivo não encontrado.");
//        //}
//    }

//    static UserCredential AuthenticateUser(string userEmail)
//    {
//        using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
//        {
//            string tokenPath = "tokens/" + userEmail;
//            return GoogleWebAuthorizationBroker.AuthorizeAsync(
//                GoogleClientSecrets.FromStream(stream).Secrets,
//                Scopes,
//                userEmail,
//                CancellationToken.None,
//                new FileDataStore(tokenPath, true)).Result;
//        }
//    }

//    // 🔹 Verifica se a pasta "saves" existe no Google Drive. Se não existir, cria
//    static string GetOrCreateFolder(DriveService service, string folderName, string parentFolderId = null)
//    {
//        FilesResource.ListRequest listRequest = service.Files.List();
//        listRequest.Q = $"name = '{folderName}' and mimeType = 'application/vnd.google-apps.folder'" +
//                        (parentFolderId != null ? $" and '{parentFolderId}' in parents" : "");
//        listRequest.Fields = "files(id)";

//        var folder = listRequest.Execute().Files.FirstOrDefault();
//        if (folder != null) return folder.Id;

//        var newFolder = new Google.Apis.Drive.v3.Data.File()
//        {
//            Name = folderName,
//            MimeType = "application/vnd.google-apps.folder",
//            Parents = parentFolderId != null ? new List<string> { parentFolderId } : null
//        };

//        var request = service.Files.Create(newFolder);
//        request.Fields = "id";
//        var createdFolder = request.Execute();

//        Console.WriteLine($"Pasta '{folderName}' criada! ID: {createdFolder.Id}");

//        return createdFolder.Id;
//    }

//    // 🔹 Sincroniza os arquivos da pasta "saves"
//    static void SyncSaves(DriveService service, string folderId, string localFolderPath)
//    {
//        if (!Directory.Exists(localFolderPath))
//        {
//            Console.WriteLine($"Criando pasta local: {localFolderPath}");
//            Directory.CreateDirectory(localFolderPath);
//        }

//        // Obtém a lista de arquivos na pasta do Google Drive
//        FilesResource.ListRequest listRequest = service.Files.List();
//        listRequest.Q = $"'{folderId}' in parents";
//        listRequest.Fields = "files(id, name, modifiedTime, md5Checksum)";

//        var driveFiles = listRequest.Execute().Files;
//        var driveFileMap = driveFiles.ToDictionary(f => f.Name, f => f);

//        // Verifica arquivos locais e compara com os do Drive
//        foreach (var localFile in Directory.GetFiles(localFolderPath))
//        {
//            string fileName = Path.GetFileName(localFile);
//            string localHash = ComputeMD5(localFile);

//            if (driveFileMap.TryGetValue(fileName, out var driveFile))
//            {
//                // Compara as datas de modificação
//                DateTime localFileDate = File.GetLastWriteTimeUtc(localFile);
//                DateTime driveFileDate = DateTime.Parse(driveFile.ModifiedTimeRaw, null, DateTimeStyles.RoundtripKind);

//                Console.WriteLine($"🔹 Comparando '{fileName}':");
//                Console.WriteLine($"   📂 Local:  {localFileDate} UTC (MD5: {localHash})");
//                Console.WriteLine($"   ☁️  Drive: {driveFileDate} UTC (MD5: {driveFile.Md5Checksum})");

//                // Se os hashes são iguais, não sincroniza
//                if (localHash == driveFile.Md5Checksum)
//                {
//                    Console.WriteLine($"✅ Arquivo '{fileName}' já é idêntico. Nada a fazer.");
//                    continue;
//                }

//                // Se a diferença de tempo for muito pequena, assume que são iguais
//                if (Math.Abs((localFileDate - driveFileDate).TotalSeconds) < 120)
//                {
//                    Console.WriteLine($"🔄 Diferença de tempo pequena para '{fileName}', ignorando...");
//                    continue;
//                }

//                if (localFileDate > driveFileDate)
//                {
//                    Console.WriteLine($"📤 Arquivo local '{fileName}' é mais recente. Enviando para o Drive...");
//                    UploadFile(service, localFile, folderId, driveFile.Id); // Atualiza o arquivo no Drive
//                }
//                else if (driveFileDate > localFileDate)
//                {
//                    Console.WriteLine($"📥 Arquivo '{fileName}' no Google Drive é mais recente. Baixando...");
//                    DownloadFile(service, driveFile.Id, localFile);
//                }
//                else
//                {
//                    Console.WriteLine($"✅ Arquivo '{fileName}' já está atualizado.");
//                }
//            }
//            else
//            {
//                Console.WriteLine($"📤 Arquivo '{fileName}' não existe no Drive. Enviando...");
//                UploadFile(service, localFile, folderId);
//            }
//        }

//        // Verifica se há arquivos no Drive que não estão na máquina local
//        foreach (var driveFile in driveFiles)
//        {
//            string localFilePath = Path.Combine(localFolderPath, driveFile.Name);
//            if (!File.Exists(localFilePath))
//            {
//                Console.WriteLine($"📥 Arquivo '{driveFile.Name}' não existe localmente. Baixando...");
//                DownloadFile(service, driveFile.Id, localFilePath);
//            }
//        }
//    }

//    // 🔹 Faz upload do arquivo para o Google Drive (se já existe, atualiza)
//    static void UploadFile(DriveService service, string filePath, string folderId, string existingFileId = null)
//    {
//        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
//        {
//            Name = Path.GetFileName(filePath),
//            //Parents = new List<string> { folderId }
//        };

//        using (var stream = new FileStream(filePath, FileMode.Open))
//        {
//            if (existingFileId != null)
//            {
//                // Atualiza o arquivo existente
//                var updateRequest = service.Files.Update(fileMetadata, existingFileId, stream, "application/octet-stream");
//                var progress = updateRequest.Upload();
//                if (progress.Exception != null)
//                {
//                    var msg = progress.Exception.Message.ToString();
//                }

//                Console.WriteLine($"✅ Arquivo '{fileMetadata.Name}' atualizado no Google Drive.");
//            }
//            else
//            {
//                // Faz o upload do novo arquivo
//                var createRequest = service.Files.Create(fileMetadata, stream, "application/octet-stream");
//                createRequest.Upload();
//                Console.WriteLine($"✅ Arquivo '{fileMetadata.Name}' enviado para o Google Drive.");
//            }
//        }
//    }

//    // 🔹 Faz o download do arquivo do Google Drive para a máquina local
//    static void DownloadFile(DriveService service, string fileId, string localFilePath)
//    {
//        try
//        {
//            var request = service.Files.Get(fileId);
//            using (var stream = new FileStream(localFilePath, FileMode.Create))
//            {
//                request.Download(stream);
//            }

//            Console.WriteLine($"✅ Arquivo '{localFilePath}' baixado com sucesso!");
//        }
//        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
//        {
//            Console.WriteLine($"❌ Erro: Arquivo não encontrado no Google Drive. (404 Not Found)");
//        }
//    }

//    static string ComputeMD5(string filePath)
//    {
//        using (var md5 = MD5.Create())
//        {
//            using (var stream = File.OpenRead(filePath))
//            {
//                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
//            }
//        }
//    }

//    static void UploadFile(DriveService service, string filePath)
//    {
//        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
//        {
//            Name = Path.GetFileName(filePath)
//        };

//        FilesResource.CreateMediaUpload request;
//        using (var stream = new FileStream(filePath, FileMode.Open))
//        {
//            request = service.Files.Create(fileMetadata, stream, "application/octet-stream");
//            request.Fields = "id";
//            request.Upload();
//        }

//        Console.WriteLine($"Arquivo enviado com sucesso! ID: {request.ResponseBody.Id}");
//    }

//    // Função para comparar arquivos e atualizar a pasta
//    static void CompareAndUpdateFolder(DriveService service, string folderId, string localFolderPath)
//    {
//        // Lista todos os arquivos da pasta no Google Drive
//        FilesResource.ListRequest listRequest = service.Files.List();
//        listRequest.Q = $"'{folderId}' in parents"; // Filtro para listar arquivos da pasta
//        listRequest.Fields = "files(id, name, modifiedTime)";

//        var fileList = listRequest.Execute();

//        foreach (var file in fileList.Files)
//        {
//            string localFilePath = Path.Combine(localFolderPath, file.Name);

//            if (File.Exists(localFilePath))
//            {
//                // Verifica se o arquivo local está desatualizado
//                DateTime localFileDate = File.GetLastWriteTime(localFilePath).ToUniversalTime();
//                DateTime driveFileDate = file.ModifiedTimeDateTimeOffset.HasValue ?
//                                            file.ModifiedTimeDateTimeOffset.Value.UtcDateTime :
//                                            DateTime.MinValue;

//                if (driveFileDate > localFileDate)
//                {
//                    Console.WriteLine($"O arquivo '{file.Name}' no Google Drive é mais recente. Atualizando...");
//                    DownloadFile(service, file.Id, localFilePath);
//                }
//            }
//            else
//            {
//                Console.WriteLine($"O arquivo '{file.Name}' não existe localmente. Baixando...");
//                DownloadFile(service, file.Id, localFilePath);
//            }
//        }
//    }

//    //// Função para verificar e comparar um arquivo específico
//    //static void CheckAndUpdateFile(DriveService service, string fileName, string localFilePath)
//    //{
//    //    FilesResource.ListRequest listRequest = service.Files.List();
//    //    listRequest.Q = $"name = '{fileName}'"; // Pesquisa pelo nome do arquivo
//    //    listRequest.Fields = "files(id, name, modifiedTime)";

//    //    var fileList = listRequest.Execute();

//    //    var file = fileList.Files.FirstOrDefault();
//    //    if (file != null)
//    //    {
//    //        if (File.Exists(localFilePath))
//    //        {
//    //            DateTime localFileDate = File.GetLastWriteTime(localFilePath);
//    //            DateTime driveFileDate = Convert.ToDateTime(file.ModifiedTimeDateTimeOffset);

//    //            if (driveFileDate > localFileDate)
//    //            {
//    //                Console.WriteLine($"O arquivo '{fileName}' no Google Drive é mais recente. Atualizando...");
//    //                DownloadFile(service, file.Id, localFilePath);
//    //            }
//    //        }
//    //        else
//    //        {
//    //            Console.WriteLine($"O arquivo '{fileName}' não existe localmente. Baixando...");
//    //            DownloadFile(service, file.Id, localFilePath);
//    //        }
//    //    }
//    //    else
//    //    {
//    //        Console.WriteLine($"O arquivo '{fileName}' não foi encontrado no Google Drive.");
//    //    }
//    //}

//    //// Função para fazer o download do arquivo
//    //static void DownloadFile(DriveService service, string fileId, string localFilePath)
//    //{
//    //    var request = service.Files.Get(fileId);
//    //    using (var stream = new FileStream(localFilePath, FileMode.Create))
//    //    {
//    //        request.Download(stream);
//    //    }

//    //    Console.WriteLine($"Arquivo '{localFilePath}' baixado com sucesso!");
//    //}
//}
