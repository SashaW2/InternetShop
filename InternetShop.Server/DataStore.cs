using System;
using System.Collections.Generic;
using InternetShop.Shared.Models;
using InternetShop.Shared.Enums;

namespace InternetShop.Server
{
    public static class DataStore
    {
        private static List<Product> _products = new List<Product>();
        private static List<Order> _orders = new List<Order>();
        private static List<Customer> _customers = new List<Customer>();
        private static List<Payment> _payments = new List<Payment>();
        private static int _orderIdCounter = 1;
        private static int _paymentIdCounter = 1;

        public static List<Product> Products
        {
            get
            {
                if (_products.Count == 0)
                    InitializeProducts();
                return _products;
            }
            set => _products = value;
        }

        public static List<Order> Orders
        {
            get => _orders;
            set => _orders = value;
        }

        public static List<Customer> Customers
        {
            get
            {
                if (_customers.Count == 0)
                    InitializeCustomers();
                return _customers;
            }
            set => _customers = value;
        }

        public static List<Payment> Payments
        {
            get => _payments;
            set => _payments = value;
        }

        private static void InitializeProducts()
        {
            _products = new List<Product>
            {
                new Product {
                    Id = 1,
                    Name = "Ноутбук ASUS",
                    Description = "15.6\", Intel Core i5, 8GB RAM",
                    Price = 1899.99m,
                    Stock = 10,
                    Category = "Электроника"
                },
                new Product {
                    Id = 2,
                    Name = "Смартфон Samsung",
                    Description = "6.4\", 128GB, 5G",
                    Price = 1099.99m,
                    Stock = 25,
                    Category = "Электроника"
                },
                new Product {
                    Id = 3,
                    Name = "Наушники Sony",
                    Description = "Беспроводные, шумоподавление",
                    Price = 299.99m,
                    Stock = 50,
                    Category = "Аксессуары"
                },
                new Product {
                    Id = 4,
                    Name = "Книга C# в действии",
                    Description = "Изучение C# на практике",
                    Price = 45.99m,
                    Stock = 30,
                    Category = "Книги"
                },
                new Product {
                    Id = 5,
                    Name = "Клавиатура Logitech",
                    Description = "Механическая, RGB подсветка",
                    Price = 199.99m,
                    Stock = 15,
                    Category = "Аксессуары"
                }
            };
        }

        private static void InitializeCustomers()
        {
            _customers = new List<Customer>
            {
                new Customer {
                    Id = 1,
                    FirstName = "Иван",
                    LastName = "Петров",
                    Email = "ivan@mail.ru",
                    Phone = "+375-29-123-45-67",
                    RegisteredAt = DateTime.Now.AddDays(-30)
                },
                new Customer {
                    Id = 2,
                    FirstName = "Мария",
                    LastName = "Иванова",
                    Email = "maria@mail.ru",
                    Phone = "+375-29-765-43-21",
                    RegisteredAt = DateTime.Now.AddDays(-15)
                }
            };
        }

        public static int GetNextOrderId()
        {
            return _orderIdCounter++;
        }

        public static int GetNextPaymentId()
        {
            return _paymentIdCounter++;
        }

        public static void ResetData()
        {
            _products.Clear();
            _orders.Clear();
            _customers.Clear();
            _payments.Clear();
            _orderIdCounter = 1;
            _paymentIdCounter = 1;

            InitializeProducts();
            InitializeCustomers();
        }
    }
}