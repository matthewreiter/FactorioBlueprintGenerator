using Music;
using FactoVision.Runtime;
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
        var songAddress = GetSongAddress(requestedSong);

        if (isLow)
        {
            previousPosition = songAddress;
        }
        else
        {
            MusicBox.Position = songAddress;
        }
    }

    if (charge < 30 && !isLow)
    {
        var cynthia = GetSongAddress(SongMetadataAddresses.Cynthia);

        previousPosition = MusicBox.Position;
        MusicBox.Position = cynthia;
    }
    else if (charge > 40 && isLow)
    {
        MusicBox.Position = previousPosition;
        Thread.Sleep(10000);
    }

    NumericDisplay.Value = MusicBox.GetPlayingSongMetadata(MusicBox.SongMetadataField.TrackNumber);
}

static int GetSongAddress(int metadataAddress)
{
    MusicBox.SelectSongMetadata(metadataAddress);
    return MusicBox.GetSelectedSongMetadata(MusicBox.SongMetadataField.SongAddress);
}
