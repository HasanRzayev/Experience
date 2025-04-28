namespace ExperienceProject.Models;
public enum FollowRequestStatus
{
    Pending,
    Accepted,
    Rejected
}
public class FollowRequest
{
    public int Id { get; set; }
    public int FollowerId { get; set; }
    public User Follower { get; set; } // Naviqasiya properti

    public int FollowedId { get; set; }
    public User Followed { get; set; } // Naviqasiya properti

    public FollowRequestStatus Status { get; set; }
}
