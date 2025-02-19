namespace SaveSync.Models
{
    public class Profile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<Game> Games { get; set; } = new List<Game>();
    }
}
