using Music;
using SeeSharp.Runtime;
using System.Threading;

var previousPosition = 0;

while (true)
{
    var charge = FactoryNetwork.GetValue(Signal.A);
    var currentSong = MusicBox.GetPlayingSongMetadata(MusicBox.SongMetadataField.MetadataAddress);
    var isLow = currentSong == SongMetadataAddresses.Cynthia;
    var requestedSong = MusicBox.RequestedSong;

    if (requestedSong > 0 && requestedSong != currentSong)
    {
        MusicBox.SelectSongMetadata(requestedSong);
        var songAddress = MusicBox.GetSelectedSongMetadata(MusicBox.SongMetadataField.SongAddress);

        if (isLow)
        {
            previousPosition = songAddress;
        }
        else
        {
            SetPosition(songAddress);
        }
    }

    if (charge < 50 && !isLow)
    {
        MusicBox.SelectSongMetadata(SongMetadataAddresses.Cynthia);
        var cynthia = MusicBox.GetSelectedSongMetadata(MusicBox.SongMetadataField.SongAddress);

        previousPosition = MusicBox.Position;
        SetPosition(cynthia);
    }
    else if (charge > 55 && isLow)
    {
        SetPosition(previousPosition);
    }

    NumericDisplay.Value = MusicBox.GetPlayingSongMetadata(MusicBox.SongMetadataField.TrackNumber);
}

void SetPosition(int position)
{
    MusicBox.Position = position;
    Thread.Sleep(10000);
}
