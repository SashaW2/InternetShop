using System;

namespace InternetShop.Shared.DTO
{
    public class TimeResponse
    {
        // Время сервера (UTC), зафиксированное на стороне сервера
        public DateTime ServerTimeUtc { get; set; }

        // Искусственная задержка, примененная сервером (мс)
        public int ArtificialDelayMs { get; set; }
    }
}