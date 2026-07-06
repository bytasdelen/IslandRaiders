using Unity.Netcode;

// oyuncunun üzerine binebilecegi her gemi turunun ortak arayuzu (oyuncu tekne + ai gemi)
public interface IShipDeck
{
    NetworkObject NetworkObject { get; }
}
