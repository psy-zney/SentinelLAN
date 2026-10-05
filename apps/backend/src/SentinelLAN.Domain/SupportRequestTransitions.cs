namespace SentinelLAN.Domain;

public static class SupportRequestTransitions
{
    public static bool CanChange(string kind, string current, string next, bool employee)
    {
        var canReopen = kind is "Incident" or "Appointment" or "Panic";
        if (employee)
            return next switch
            {
                "Closed" => current is "AwaitingEmployee" or "Resolved" or "Closed",
                "Open" => canReopen && current is "AwaitingEmployee" or "Resolved" or "Closed",
                _ => false
            };
        if (current == next) return current is "Open" or "InProgress" or "Approved" or "Rejected" or "AwaitingEmployee" or "Resolved" or "Closed";
        return current switch
        {
            "Open" => canReopen && next == "InProgress",
            "InProgress" => next is "AwaitingEmployee" or "Resolved",
            "Approved" => next is "InProgress" or "AwaitingEmployee" or "Resolved",
            "AwaitingEmployee" => next is "Resolved" or "Closed" or "InProgress" || canReopen && next == "Open",
            "Resolved" => next == "Closed" || canReopen && next == "Open",
            "Closed" => canReopen && next == "Open",
            _ => false
        };
    }
}
