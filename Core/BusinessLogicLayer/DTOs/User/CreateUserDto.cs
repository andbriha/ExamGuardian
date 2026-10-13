using System;
using System.Collections.Generic;
using System.Text;

namespace BusinessLogicLayer.DTOs.User
{
    public class CreateUserDto
    {
        public string FullName { get; set; } = null!;
        public string Login { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string? Group { get; set; }
    }
}
