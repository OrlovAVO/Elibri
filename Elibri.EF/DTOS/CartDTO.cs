﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elibri.EF.DTOS
{
    public class CartDTO
    {
        [Key]
        [Required]
        public int CartId { get; set; }

        [Required]
        public string UserId { get; set; }

        public List<CartItemDTO> CartItems { get; set; } = new List<CartItemDTO>();
    }
}
