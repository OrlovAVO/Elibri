﻿using AutoMapper;
using Elibri.EF.DTOS;
using Elibri.EF.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Elibri.Core.Features.OrderServices
{
    public class OrderService : IOrderService
    {
        private readonly Context _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;

        public OrderService(Context context, IHttpContextAccessor httpContextAccessor, IMapper mapper)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _mapper = mapper;
        }

        public async Task<List<OrderDTO>> GetAllAsync()
        {
            var orders = await _context.Orders
                .Include(order => order.OrderDetails)
                    .ThenInclude(od => od.Product)
                .ToListAsync();

            return orders.Select(order =>
            {
                var orderDto = _mapper.Map<OrderDTO>(order);

                foreach (var cartItem in orderDto.CartItems)
                {
                    var product = order.OrderDetails.FirstOrDefault(od => od.ProductId == cartItem.ProductId)?.Product;
                    if (product != null)
                    {
                        cartItem.Image = product.Image;
                    }
                }

                return orderDto;
            }).ToList();
        }

        public async Task<List<OrderDTO>> GetOrdersByUserIdAsync(string userId)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Where(o => o.UserId == userId)
                .ToListAsync();

            var orderDTOs = orders.Select(order =>
            {
                var orderDto = _mapper.Map<OrderDTO>(order);

                foreach (var cartItem in orderDto.CartItems)
                {
                    var product = order.OrderDetails.FirstOrDefault(od => od.ProductId == cartItem.ProductId)?.Product;
                    if (product != null)
                    {
                        cartItem.Image = product.Image;
                    }
                }

                return orderDto;
            }).ToList();

            return orderDTOs;
        }


        public async Task<ServiceResult<OrderDTO>> CreateOrderAsync(CreateOrderDTO createOrderDto)
        {
            var userId = _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
            {
                return new ServiceResult<OrderDTO> { IsSuccess = false, ErrorMessage = "User not authenticated." };
            }

            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.UtcNow,
                FirstName = createOrderDto.FirstName,
                LastName = createOrderDto.LastName,
                Address = createOrderDto.Address,
                PhoneNumber = createOrderDto.PhoneNumber,
                CardNumber = createOrderDto.CardNumber
            };

            _context.Orders.Add(order);

            decimal totalAmount = 0;

            foreach (var cartItem in createOrderDto.CartItems)
            {
                var product = await _context.Products.FindAsync(cartItem.ProductId);
                if (product == null)
                {
                    return new ServiceResult<OrderDTO> { IsSuccess = false, ErrorMessage = $"Product with ID:{cartItem.ProductId} not found." };
                }

                if (product.StockQuantity < cartItem.Quantity)
                {
                    return new ServiceResult<OrderDTO> { IsSuccess = false, ErrorMessage = $"Insufficient stock for product with ID:{cartItem.ProductId}." };
                }

                product.StockQuantity -= cartItem.Quantity;

                var unitPrice = product.Price;
                var itemTotal = unitPrice * cartItem.Quantity;

                totalAmount += itemTotal;

                var orderDetail = new OrderDetail
                {
                    Order = order,
                    ProductId = cartItem.ProductId,
                    StockQuantity = cartItem.Quantity,
                    TotalPrice = itemTotal
                };

                _context.OrderDetails.Add(orderDetail);
            }

            order.TotalPrice = totalAmount;

            await _context.SaveChangesAsync();

            var orderDTO = _mapper.Map<OrderDTO>(order);

            return new ServiceResult<OrderDTO> { IsSuccess = true, Data = orderDTO };
        }


        public async Task DeleteAsync(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return;

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
        }
    }

    public class ServiceResult<T>
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public T Data { get; set; }
    }
}
