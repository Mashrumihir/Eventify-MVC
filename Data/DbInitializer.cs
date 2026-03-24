using Eventify.Models;

namespace Eventify.Data;

public static class DbInitializer
{
    public static void Seed(EventifyDbContext context)
    {
        if (context.Events.Any())
        {
            return;
        }

        var events = new List<EventItem>
        {
            new()
            {
                Title = "Tech Summit 2026",
                Category = "Technology",
                StartDateTime = new DateTime(2026, 3, 15, 9, 0, 0),
                Location = "Marvadi University, Rajkot",
                Price = 99,
                ImageUrl = "https://images.unsplash.com/photo-1511578314322-379afb476865?w=1400",
                Rating = 4.8,
                ReviewCount = 2347,
                AttendingCount = 2847,
                ShortDescription = "Connect with industry leaders and explore cutting-edge innovations.",
                Description = "Join the most anticipated technology summit of 2026. Connect with industry leaders, discover cutting-edge innovations, and network with thousands of tech professionals from around the world."
            },
            new()
            {
                Title = "Summer Music Festival 2024",
                Category = "Music",
                StartDateTime = new DateTime(2026, 6, 15, 18, 0, 0),
                Location = "Central Mumbai, Maharashtra",
                Price = 45,
                ImageUrl = "https://images.unsplash.com/photo-1492684223066-81342ee5ff30?w=1200",
                Rating = 4.8,
                ReviewCount = 234,
                AttendingCount = 1200,
                ShortDescription = "Live performances from top artists.",
                Description = "Experience an electric evening with live bands, light shows, and unforgettable performances."
            },
            new()
            {
                Title = "Modern Art Exhibition",
                Category = "Arts",
                StartDateTime = new DateTime(2026, 6, 10, 10, 0, 0),
                Location = "Ahmedabad, Gujarat",
                Price = 0,
                ImageUrl = "https://images.unsplash.com/photo-1547891654-e66ed7ebb968?w=1200",
                Rating = 4.7,
                ReviewCount = 89,
                AttendingCount = 800,
                ShortDescription = "Contemporary artwork from global creators.",
                Description = "Explore curated galleries and immersive installations from emerging and established artists."
            },
            new()
            {
                Title = "Championship Basketball",
                Category = "Sports",
                StartDateTime = new DateTime(2026, 8, 5, 19, 30, 0),
                Location = "R.K. University, Rajkot",
                Price = 85,
                ImageUrl = "https://images.unsplash.com/photo-1546519638-68e109498ffc?w=1200",
                Rating = 4.9,
                ReviewCount = 678,
                AttendingCount = 2400,
                ShortDescription = "High intensity championship finals.",
                Description = "Watch elite teams compete in an action-packed basketball final."
            },
            new()
            {
                Title = "International Food Festival",
                Category = "Food",
                StartDateTime = new DateTime(2026, 7, 8, 11, 0, 0),
                Location = "Reshkosh, Rajkot",
                Price = 30,
                ImageUrl = "https://images.unsplash.com/photo-1414235077428-338989a2e8c0?w=1200",
                Rating = 4.6,
                ReviewCount = 345,
                AttendingCount = 980,
                ShortDescription = "Taste cuisines from around the world.",
                Description = "Sample global street food, chef specials, and live culinary demos."
            },
            new()
            {
                Title = "Startup Networking Event",
                Category = "Business",
                StartDateTime = new DateTime(2026, 6, 28, 18, 0, 0),
                Location = "R.K. University, Rajkot",
                Price = 0,
                ImageUrl = "https://images.unsplash.com/photo-1511795409834-ef04bbd61622?w=1200",
                Rating = 4.8,
                ReviewCount = 156,
                AttendingCount = 1100,
                ShortDescription = "Meet founders, investors, and builders.",
                Description = "Expand your network with startup founders, product leaders, and investors."
            },
            new()
            {
                Title = "Jazz Night Live",
                Category = "Music",
                StartDateTime = new DateTime(2026, 7, 12, 20, 0, 0),
                Location = "Hemu Gadhvi Hall, Rajkot",
                Price = 55,
                ImageUrl = "https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?w=1200",
                Rating = 5.0,
                ReviewCount = 423,
                AttendingCount = 760,
                ShortDescription = "An intimate evening of live jazz.",
                Description = "Enjoy classic and modern jazz sets by acclaimed performers."
            },
            new()
            {
                Title = "VR & AI Experience",
                Category = "Technology",
                StartDateTime = new DateTime(2026, 8, 18, 14, 0, 0),
                Location = "Jaipur, Rajasthan",
                Price = 75,
                ImageUrl = "https://images.unsplash.com/photo-1592478411213-6153e4ebc696?w=1200",
                Rating = 4.9,
                ReviewCount = 267,
                AttendingCount = 1330,
                ShortDescription = "Hands-on demos of immersive tech.",
                Description = "Discover practical applications of AI and VR through interactive product demos."
            }
        };

        context.Events.AddRange(events);
        context.SaveChanges();

        var techSummitId = context.Events.First(e => e.Title == "Tech Summit 2026").Id;

        context.TicketOptions.AddRange(
            new TicketOption
            {
                EventItemId = techSummitId,
                Name = "Early Bird",
                Price = 79,
                Features = "Access to all sessions|Welcome kit|Networking lunch"
            },
            new TicketOption
            {
                EventItemId = techSummitId,
                Name = "Regular",
                Price = 99,
                Features = "Access to all sessions|Welcome kit|Networking lunch|Certificate"
            },
            new TicketOption
            {
                EventItemId = techSummitId,
                Name = "VIP",
                Price = 199,
                Features = "All Regular benefits|VIP seating|Meet & greet|Exclusive dinner"
            });

        context.Speakers.AddRange(
            new EventSpeaker
            {
                EventItemId = techSummitId,
                Name = "Alex Chen",
                Role = "CTO, TechVision",
                ImageUrl = "https://images.unsplash.com/photo-1560250097-0b93528c311a?w=300"
            },
            new EventSpeaker
            {
                EventItemId = techSummitId,
                Name = "Sarah Johnson",
                Role = "AI Researcher",
                ImageUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=300"
            },
            new EventSpeaker
            {
                EventItemId = techSummitId,
                Name = "Michael Roberts",
                Role = "Startup Founder",
                ImageUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=300"
            });

        context.Reviews.AddRange(
            new EventReview
            {
                EventItemId = techSummitId,
                UserName = "User",
                Rating = 5,
                Comment = "Amazing event with great speakers and networking opportunities!",
                TimeAgo = "2 days ago",
                AvatarUrl = "https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?w=200"
            },
            new EventReview
            {
                EventItemId = techSummitId,
                UserName = "User",
                Rating = 4,
                Comment = "Well organized and informative. Definitely worth attending.",
                TimeAgo = "1 week ago",
                AvatarUrl = "https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=200"
            });

        context.SaveChanges();

        if (!context.Bookings.Any())
        {
            var eventIds = context.Events.Select(e => e.Id).ToList();
            var demoBookings = new List<Booking>();
            for (var i = 0; i < 24; i++)
            {
                demoBookings.Add(new Booking
                {
                    EventItemId = eventIds[i % eventIds.Count],
                    UserEmail = "attend@eventify.com",
                    Status = i % 10 == 0 ? "Canceled" : "Booked",
                    IsSaved = i % 2 == 0
                });
            }

            context.Bookings.AddRange(demoBookings);
            context.SaveChanges();
        }
    }
}
