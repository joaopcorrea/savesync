using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Globalization;
using System.Security.Cryptography;

public class LoginController
{
    static string[] Scopes = { DriveService.Scope.DriveFile };
    static string ApplicationName = "Google Drive Upload";

    public async Task Login(string email, string diretorio, Progress<int> progress)
    {
        Console.Write("Informe seu e-mail do Google: ");
        string userEmail = email;

        UserCredential credential = AuthenticateUser(userEmail);
        var service = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });

        Console.Write("Informe o caminho do arquivo para upload: ");
        string filePath = diretorio;


        string rootFolderName = "savesync";
        string subFolderName = "saves";
        string localFolderPath = @"D:\savesync-saves"; // Caminho da pasta local

        string rootFolderId = GetOrCreateFolder(service, rootFolderName);
        string subFolderId = GetOrCreateFolder(service, subFolderName, rootFolderId);

        await SyncSavesAsync(service, subFolderId, localFolderPath, progress);
    }

    static UserCredential AuthenticateUser(string userEmail)
    {
        using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
        {
            string tokenPath = "tokens/" + userEmail;
            return GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                userEmail,
                CancellationToken.None,
                new FileDataStore(tokenPath, true)).Result;
        }
    }

    // 🔹 Verifica se a pasta "saves" existe no Google Drive. Se não existir, cria
    static string GetOrCreateFolder(DriveService service, string folderName, string parentFolderId = null)
    {
        FilesResource.ListRequest listRequest = service.Files.List();
        listRequest.Q = $"name = '{folderName}' and mimeType = 'application/vnd.google-apps.folder'" +
                        (parentFolderId != null ? $" and '{parentFolderId}' in parents" : "");
        listRequest.Fields = "files(id)";

        var folder = listRequest.Execute().Files.FirstOrDefault();
        if (folder != null) return folder.Id;

        var newFolder = new Google.Apis.Drive.v3.Data.File()
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = parentFolderId != null ? new List<string> { parentFolderId } : null
        };

        var request = service.Files.Create(newFolder);
        request.Fields = "id";
        var createdFolder = request.Execute();

        Console.WriteLine($"Pasta '{folderName}' criada! ID: {createdFolder.Id}");

        return createdFolder.Id;
    }

    // 🔹 Sincroniza os arquivos da pasta "saves" de forma assíncrona
    public static async Task SyncSavesAsync(DriveService service, string folderId, string localFolderPath, IProgress<int> progress)
    {
        if (!Directory.Exists(localFolderPath))
        {
            Console.WriteLine($"Criando pasta local: {localFolderPath}");
            Directory.CreateDirectory(localFolderPath);
        }

        // Obtém a lista de arquivos na pasta do Google Drive
        FilesResource.ListRequest listRequest = service.Files.List();
        listRequest.Q = $"'{folderId}' in parents";
        listRequest.Fields = "files(id, name, modifiedTime, md5Checksum)";

        var driveFiles = await listRequest.ExecuteAsync();
        var driveFileMap = driveFiles.Files.ToDictionary(f => f.Name, f => f);

        var localFiles = Directory.GetFiles(localFolderPath);
        int totalFiles = localFiles.Length + driveFiles.Files.Count;
        int processedFiles = 0;

        // Verifica arquivos locais e compara com os do Drive
        foreach (var localFile in localFiles)
        {
            string fileName = Path.GetFileName(localFile);
            string localHash = ComputeMD5(localFile);

            if (driveFileMap.TryGetValue(fileName, out var driveFile))
            {
                // Compara as datas de modificação
                DateTime localFileDate = File.GetLastWriteTimeUtc(localFile);
                DateTime driveFileDate = DateTime.Parse(driveFile.ModifiedTimeRaw, null, DateTimeStyles.RoundtripKind);

                Console.WriteLine($"🔹 Comparando '{fileName}':");
                Console.WriteLine($"   📂 Local:  {localFileDate} UTC (MD5: {localHash})");
                Console.WriteLine($"   ☁️  Drive: {driveFileDate} UTC (MD5: {driveFile.Md5Checksum})");

                // Se os hashes são iguais, não sincroniza
                if (localHash == driveFile.Md5Checksum)
                {
                    Console.WriteLine($"✅ Arquivo '{fileName}' já é idêntico. Nada a fazer.");
                    continue;
                }

                // Se a diferença de tempo for muito pequena, assume que são iguais
                if (Math.Abs((localFileDate - driveFileDate).TotalSeconds) < 120)
                {
                    Console.WriteLine($"🔄 Diferença de tempo pequena para '{fileName}', ignorando...");
                    continue;
                }

                if (localFileDate > driveFileDate)
                {
                    Console.WriteLine($"📤 Arquivo local '{fileName}' é mais recente. Enviando para o Drive...");
                    await UploadFileAsync(service, localFile, folderId, driveFile.Id); // Atualiza o arquivo no Drive
                }
                else if (driveFileDate > localFileDate)
                {
                    Console.WriteLine($"📥 Arquivo '{fileName}' no Google Drive é mais recente. Baixando...");
                    await DownloadFileAsync(service, driveFile.Id, localFile);
                }
                else
                {
                    Console.WriteLine($"✅ Arquivo '{fileName}' já está atualizado.");
                }
            }
            else
            {
                Console.WriteLine($"📤 Arquivo '{fileName}' não existe no Drive. Enviando...");
                await UploadFileAsync(service, localFile, folderId);
            }

            processedFiles++;
            progress.Report((int)((double)processedFiles / totalFiles * 100)); // Reporta progresso
        }

        // Verifica se há arquivos no Drive que não estão na máquina local
        foreach (var driveFile in driveFiles.Files)
        {
            string localFilePath = Path.Combine(localFolderPath, driveFile.Name);
            if (!File.Exists(localFilePath))
            {
                Console.WriteLine($"📥 Arquivo '{driveFile.Name}' não existe localmente. Baixando...");
                await DownloadFileAsync(service, driveFile.Id, localFilePath);
            }

            processedFiles++;
            progress.Report((int)((double)processedFiles / totalFiles * 100));
        }
    }

    // 🔹 Faz upload do arquivo para o Google Drive (se já existe, atualiza)
    static async Task UploadFileAsync(DriveService service, string filePath, string folderId, string existingFileId = null)
    {
        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = Path.GetFileName(filePath),
            Parents = existingFileId == null ? new List<string> { folderId } : null
        };

        using (var stream = new FileStream(filePath, FileMode.Open))
        {
            if (existingFileId != null)
            {
                // Atualiza o arquivo existente
                var updateRequest = service.Files.Update(fileMetadata, existingFileId, stream, "application/octet-stream");
                var progress = await updateRequest.UploadAsync();
                if (progress.Exception != null)
                {
                    var msg = $"Erro ao atualizar o arquivo: {progress.Exception.Message}";
                    Console.WriteLine(msg);
                }
                else
                    Console.WriteLine($"✅ Arquivo '{fileMetadata.Name}' atualizado no Google Drive.");
            }
            else
            {
                // Faz o upload do novo arquivo
                var createRequest = service.Files.Create(fileMetadata, stream, "application/octet-stream");
                await createRequest.UploadAsync();
                Console.WriteLine($"✅ Arquivo '{fileMetadata.Name}' enviado para o Google Drive.");
            }
        }
    }

    // 🔹 Faz o download do arquivo do Google Drive para a máquina local
    static async Task DownloadFileAsync(DriveService service, string fileId, string localFilePath)
    {
        try
        {
            var request = service.Files.Get(fileId);
            using (var stream = new FileStream(localFilePath, FileMode.Create))
            {
                await request.DownloadAsync(stream);
            }

            Console.WriteLine($"✅ Arquivo '{localFilePath}' baixado com sucesso!");
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Console.WriteLine($"❌ Erro: Arquivo não encontrado no Google Drive. (404 Not Found)");
        }
    }

    static string ComputeMD5(string filePath)
    {
        using (var md5 = MD5.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
            }
        }
    }
}
