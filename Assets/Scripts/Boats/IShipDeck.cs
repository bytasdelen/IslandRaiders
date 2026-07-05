using Unity.Netcode;

// oyuncunun uzerine binebilecegi her gemi turunun ortak arayuzu (oyuncu tekne + ai gemi)
public interface IShipDeck
{
    NetworkObject NetworkObject { get; }
}
