using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
	public class MessageRepository : IMessageRepository
	{
		private readonly DataContext _context;
		private readonly IMapper _mapper;
		public MessageRepository(DataContext context, IMapper mapper)
		{
			_mapper = mapper;
			_context = context;
		}

		public void AddMessage(Message message)
		{
			_context.Messages.Add(message);
		}

		public void DeleteMessage(Message message)
		{
			_context.Messages.Remove(message);
		}

		public async Task<Message> GetMessage(int id)
		{
			return await _context.Messages.Include(x => x.Sender).Include(x => x.Recipient).SingleOrDefaultAsync(x => x.Id == id);
		}

		public async Task<PagedList<MessageDto>> GetMessagesForUser(MessageParams messageParams)
		{
			var query = _context.Messages.OrderByDescending(m => m.MessageSent)
							.AsQueryable();
			query = messageParams.Container switch
			{
				"Inbox" => query.Where(x => x.Recipient.UserName == messageParams.Username && !x.RecipientDeleted),
				"Outbox" => query.Where(x => x.Sender.UserName == messageParams.Username && !x.SenderDeleted),
				_ => query.Where(x => x.Recipient.UserName == messageParams.Username && !x.RecipientDeleted && x.DateRead == null)
			};

			var messages = query.ProjectTo<MessageDto>(_mapper.ConfigurationProvider);

			return await PagedList<MessageDto>.CreateAsync(messages, messageParams.PageNumber, messageParams.PageSize);


		}

		public async Task<IEnumerable<MessageDto>> GetMessageThread(string currentUsername, string recipientUsername)
		{
			var messages = await _context.Messages
								.Include(x => x.Sender).ThenInclude(p => p.Photos)
								.Include(x => x.Recipient).ThenInclude(p => p.Photos)
								.Where(x => x.Recipient.UserName == currentUsername && !x.RecipientDeleted
								&& x.Sender.UserName == recipientUsername
								|| x.Recipient.UserName == recipientUsername && x.Sender.UserName == currentUsername
								&& !x.SenderDeleted
								).OrderBy(x => x.MessageSent).ToListAsync();
			var unreadMessages = messages.Where(m => m.DateRead == null && m.Recipient.UserName == currentUsername).ToList();

			if (unreadMessages.Any())
			{
				foreach (var message in unreadMessages)
				{
					message.DateRead = DateTime.Now;
				}
				await _context.SaveChangesAsync();
			}

			return _mapper.Map<IEnumerable<MessageDto>>(messages);
		}

		public async Task<bool> SaveAllAsync()
		{
			return await _context.SaveChangesAsync() > 0;
		}
	}
}