using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using ExperienceProject.Models;
using ExperienceProject.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Experience.Dto;
using Experience.Models;
using System.Text.Json;
using System.Collections.Concurrent;

namespace ExperienceProject.Hubs
{
    public class MessageHub : Hub
    {
        private readonly ApplicationDbContext _context;

        private static ConcurrentDictionary<int, string> _userConnections = new ConcurrentDictionary<int, string>();

        public MessageHub(ApplicationDbContext context)
        {
            _context = context;
        }



        public async Task CallUser(int receiverId, bool isVideo)
        {
            int? callerId = GetUserId();
            if (callerId == null)
            {
                await Clients.Caller.SendAsync("ErrorMessage", "User ID not found.");
                return;
            }

            if (_userConnections.TryGetValue(receiverId, out var receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("IncomingCall", new { CallerId = callerId.Value, IsVideo = isVideo });
            }
        }

        public async Task AcceptCall(int callerId)
        {
            int? receiverId = GetUserId();
            if (receiverId == null)
            {
                await Clients.Caller.SendAsync("ErrorMessage", "User ID not found.");
                return;
            }

            if (_userConnections.TryGetValue(callerId, out var callerConnectionId))
            {
                await Clients.Client(callerConnectionId).SendAsync("CallAccepted", receiverId.Value);
            }
        }

        public async Task RejectCall(int callerId)
        {
            if (_userConnections.TryGetValue(callerId, out var callerConnectionId))
            {
                await Clients.Client(callerConnectionId).SendAsync("CallRejected");
            }
        }


        public async Task SendWebRTCOffer(int receiverId, string offer)
        {
            if (_userConnections.TryGetValue(receiverId, out var receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("ReceiveWebRTCOffer", offer);
            }
        }

        public async Task SendWebRTCAnswer(int callerId, string answer)
        {
            if (_userConnections.TryGetValue(callerId, out var callerConnectionId))
            {
                await Clients.Client(callerConnectionId).SendAsync("ReceiveWebRTCAnswer", answer);
            }
        }

        public async Task SendICECandidate(int receiverId, string candidate)
        {
            if (_userConnections.TryGetValue(receiverId, out var receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("ReceiveICECandidate", candidate);
            }
        }
        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();

            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("ErrorMessage", $"⚠️ User ID could not be determined for connectionId {Context.ConnectionId}");
                await base.OnConnectedAsync();
                return;
            }

            // User bazada varmı? (Bunu yoxlamaq üçün)
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId.Value);
            if (!userExists)
            {
                await Clients.Caller.SendAsync("ErrorMessage", $"❌ User with ID {userId.Value} does not exist.");
                return;
            }

            // Köhnə ConnectionId-ni silirik
            if (_userConnections.TryGetValue(userId.Value, out var oldConnectionId))
            {
                _userConnections.TryRemove(userId.Value, out _);
            }

            // Yeni ConnectionId-ni əlavə edirik
            _userConnections[userId.Value] = Context.ConnectionId;

            // Frontend-ə məlumat göndəririk
            await Clients.Caller.SendAsync("ConnectionInfo", userId.Value, Context.ConnectionId);

            await base.OnConnectedAsync();
        }



        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                _userConnections.TryRemove(userId.Value, out _);
            }
            await base.OnDisconnectedAsync(exception);
        }
        public async Task SendMessage(MessageDTO message)
        {
            try
            {
                if (message == null)
                    throw new HubException("Message object is null.");

                if (string.IsNullOrEmpty(message.Content) && string.IsNullOrEmpty(message.MediaUrl))
                    throw new HubException("Message must have content or media.");

                var newMessage = new Message
                {
                    SenderId = message.SenderId,
                    ReceiverId = message.ReceiverId,
                    Content = message.Content,
                    MediaUrl = message.MediaUrl,
                    MediaType = message.MediaType,
                    Timestamp = DateTime.UtcNow
                };

                _context.Messages.Add(newMessage);
                await _context.SaveChangesAsync();

                // Receiver üçün connectionId-ni tapırıq
                if (_userConnections.TryGetValue(message.ReceiverId, out var receiverConnectionId))
                {
                    Console.WriteLine($"✅ Sending message to Receiver {message.ReceiverId} with ConnectionId: {receiverConnectionId}");
                    await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", newMessage);
                }
                //else
                //{
                //    Console.WriteLine($"⚠️ Receiver {message.ReceiverId} is not connected.");
                //    await Clients.Caller.SendAsync("ErrorMessage", $"Receiver {message.ReceiverId} is not connected.");
                //}

                // Sender üçün connectionId-ni tapırıq
                if (_userConnections.TryGetValue(message.SenderId, out var senderConnectionId))
                {
                    Console.WriteLine($"✅ Sending message to Sender {message.SenderId} with ConnectionId: {senderConnectionId}");
                    await Clients.Client(senderConnectionId).SendAsync("ReceiveMessage", newMessage);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in SendMessage: {ex.Message}");
                // Xətanı frontend-ə göndəririk
                await Clients.Caller.SendAsync("ErrorMessage", $"Error in SendMessage: {ex.Message}");
            }
        }

        private int? GetUserId()
        {
            var httpContext = Context.GetHttpContext();
            var token = httpContext?.Request.Query["access_token"];

            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("❌ No token provided in query string.");
                return null;
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;

                return int.TryParse(userIdClaim, out var userId) ? userId : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Token parse error: {ex.Message}");
                return null;
            }
        }




        public async Task AddComment(int experienceId, CommentDto commentDTO, string token)
        {
            try
            {
                // JWT tokenini oxuyuruq
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(userIdClaim, out var userId))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid user token");
                    return;
                }

                // Experience tapılır
                var experience = await _context.Experiences.FindAsync(experienceId);
                if (experience == null)
                {
                    await Clients.Caller.SendAsync("Error", "Experience not found");
                    return;
                }

               
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    await Clients.Caller.SendAsync("Error", "User not found");
                    return;
                }

                // Yeni şərh yaradılır
                var comment = new Comment
                {
                    Content = commentDTO.Content,
                    UserId = userId,
                    ExperienceId = experienceId,
                    CreatedAt = DateTime.UtcNow,
                    ParentCommentId = commentDTO.ParentCommentId,
                    User = user // **Burada user əlavə olunur!**
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();

                // **Düzgün JSON formatında göndəririk**
                var commentResponse = new
                {
                    comment.Id,
                    comment.Content,
                    comment.UserId,
                    User = new
                    {
                        comment.User.Id,
                        comment.User.FirstName,
                        comment.User.LastName,
                                comment.User.ProfileImage // Burada profil şəklini əlavə edirik

                    },
                    comment.CreatedAt,
                    comment.ParentCommentId
                };

                // Şərh real-time olaraq göndərilir
                await Clients.Group($"Experience_{experienceId}").SendAsync("ReceiveComment", commentResponse);
            }
            catch (Exception ex)
            {
                // Xətanın log edilməsi
                await Clients.Caller.SendAsync("Error", new { message = "Failed to add comment", details = ex.Message });
            }
        }




        public async Task JoinExperienceGroup(int experienceId)
        {
            try
            {
                if (experienceId <= 0)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "❌ Invalid Experience ID");
                    return;
                }

                Console.WriteLine($"✅ User {Context.ConnectionId} is joining group: Experience_{experienceId}");

                await Groups.AddToGroupAsync(Context.ConnectionId, $"Experience_{experienceId}");

                await Clients.Caller.SendAsync("ReceiveSuccess", $"✅ Joined Experience_{experienceId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in JoinExperienceGroup: {ex.Message}");
                await Clients.Caller.SendAsync("ReceiveError", $"❌ Server error: {ex.Message}");
            }
        }

        public async Task ReactToComment(int commentId, bool isLike, string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                await Clients.Caller.SendAsync("ReceiveError", "Invalid user token");
                return;
            }

            var comment = await _context.Comments.FindAsync(commentId);
            if (comment == null)
            {
                await Clients.Caller.SendAsync("ReceiveError", "Comment not found");
                return;
            }

            var existingReaction = await _context.CommentReactions
                .FirstOrDefaultAsync(r => r.CommentId == commentId && r.UserId == userId);

            if (existingReaction != null)
            {
                // 🔹 Eyni reaksiyanı yenidən basarsa, reaksiyanı sil
                if (existingReaction.IsLike == isLike)
                {
                    _context.CommentReactions.Remove(existingReaction);
                }
                else
                {
                    existingReaction.IsLike = isLike;
                }
            }
            else
            {
                var reaction = new CommentReaction
                {
                    CommentId = commentId,
                    UserId = userId,
                    IsLike = isLike
                };
                _context.CommentReactions.Add(reaction);
            }

            // 🔹 Yenilənmiş like və dislike saylarını hesabla
            comment.LikesCount = await _context.CommentReactions.CountAsync(r => r.CommentId == commentId && r.IsLike);
            comment.DislikesCount = await _context.CommentReactions.CountAsync(r => r.CommentId == commentId && !r.IsLike);

            await _context.SaveChangesAsync();

            // 🔹 Frontend-ə yenilənmiş məlumatları göndər
            await Clients.All.SendAsync("UpdateReaction", new Dictionary<string, object>
{
    { "CommentId", commentId },
    { "LikesCount", _context.CommentReactions.Count(r => r.CommentId == commentId && r.IsLike) },
    { "DislikesCount", _context.CommentReactions.Count(r => r.CommentId == commentId && !r.IsLike) }
});



        }


        public async Task LeaveExperienceGroup(int experienceId)
        {
            try
            {
                if (experienceId <= 0)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "❌ Invalid Experience ID");
                    return;
                }

                Console.WriteLine($"ℹ️ User {Context.ConnectionId} is leaving group: Experience_{experienceId}");

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Experience_{experienceId}");

                await Clients.Caller.SendAsync("ReceiveSuccess", $"ℹ️ Left Experience_{experienceId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in LeaveExperienceGroup: {ex.Message}");
                await Clients.Caller.SendAsync("ReceiveError", $"❌ Server error: {ex.Message}");
            }
        }







    }
}