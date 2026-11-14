using Microsoft.EntityFrameworkCore;

namespace Jarvis.Database;

[PrimaryKey(nameof(Artist), nameof(Album), nameof(Title))]
public class Song
{
    public required string Title { get; set; }
    public required string Album { get; set; }
    public required string Artist { get; set; }
    public int Plays { get; set; }
    public TimeSpan ListenTime { get; set; }
}
