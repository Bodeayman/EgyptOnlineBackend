using EgyptOnline.Models;

namespace EgyptOnline.Domain.Interfaces
{
    public interface IHasLocation
    {
        LocationCoords LocationCoords { get; }
    }

}