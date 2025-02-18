using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

public class LoginController
{
    static string[] Scopes = { DriveService.Scope.DriveFile };
    static string ApplicationName = "Google Drive Upload";

    public void Login(string email, string diretorio)
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

        if (File.Exists(filePath))
        {
            UploadFile(service, filePath);
        }
        else
        {
            Console.WriteLine("Arquivo não encontrado.");
        }
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

    static void UploadFile(DriveService service, string filePath)
    {
        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = Path.GetFileName(filePath)
        };

        FilesResource.CreateMediaUpload request;
        using (var stream = new FileStream(filePath, FileMode.Open))
        {
            request = service.Files.Create(fileMetadata, stream, "application/octet-stream");
            request.Fields = "id";
            request.Upload();
        }

        Console.WriteLine($"Arquivo enviado com sucesso! ID: {request.ResponseBody.Id}");
    }
}
