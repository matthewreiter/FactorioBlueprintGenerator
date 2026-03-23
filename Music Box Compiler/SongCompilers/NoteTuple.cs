using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MusicBoxCompiler.SongCompilers;

public class NoteTuple<T>(ICollection<T> notes) : IEnumerable<T>
{
    public IEnumerator<T> GetEnumerator() => notes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => notes.GetEnumerator();

    public override bool Equals(object obj)
    {
        return obj is NoteTuple<T> other && notes.SequenceEqual(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var note in notes)
        {
            hash.Add(note);
        }

        return hash.ToHashCode();
    }
}
