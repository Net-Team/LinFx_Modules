﻿using LinFx.Domain.Uow;
using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Interfaces;
using Ordering.Domain.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Ordering.Infrastructure.Repositories
{
    public class BuyerRepository : IBuyerRepository
    {
        private readonly OrderingContext _context;
        public IUnitOfWork UnitOfWork
        {
            get { return _context; }
        }

        public BuyerRepository(OrderingContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Buyer Add(Buyer buyer)
        {
            if (buyer.IsTransient())
            {
                return _context.Buyers
                    .Add(buyer)
                    .Entity;
            }
            else
            {
                return buyer;
            }           
        }

        public Buyer Update(Buyer buyer)
        {
            return _context.Buyers
                    .Update(buyer)
                    .Entity;
        }

        public async Task<Buyer> FindAsync(string identity)
        {
            var buyer = await _context.Buyers
                .Include(b => b.PaymentMethods)
                .Where(b => b.Identity == identity)
                .SingleOrDefaultAsync();

            return buyer;
        }

        public async Task<Buyer> FindByIdAsync(int id)
        {
            var buyer = await _context.Buyers
                .Include(b => b.PaymentMethods)
                .Where(b => b.Id == id)
                .SingleOrDefaultAsync();

            return buyer;
        }
    }
}