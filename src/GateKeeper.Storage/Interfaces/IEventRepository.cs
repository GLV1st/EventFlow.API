using GateKeeper.Storage.Entities;

namespace GateKeeper.Storage.Interfaces;

public interface IEventRepository
{
    // =====================================
    // GET ALL EVENTS
    // =====================================

    Task<List<EventEntity>> GetEventsAsync();


    // =====================================
    // GET EVENT
    // =====================================

    Task<EventEntity?> GetEventAsync(
        string eventId);


    // =====================================
    // ADD EVENT
    // =====================================

    Task AddEventAsync(
        EventEntity entity);


    // =====================================
    // UPDATE EVENT
    // =====================================

    Task UpdateEventAsync(
        EventEntity entity);


    // =====================================
    // RESERVE TICKET NUMBERS
    // =====================================

    // Atomically reserves a block of ticket numbers
    // for an event.
    //
    // Returns the first number in the reserved block.
    //
    // Azure ETag concurrency protection ensures that
    // two simultaneous requests cannot reserve the
    // same numbers.

    Task<int> ReserveTicketNumbersAsync(
        string eventId,
        int quantity);


    // =====================================
    // RESERVE TRADER NUMBER
    // =====================================

    // Atomically reserves the next trader number
    // for an event.
    //
    // Returns the reserved trader number.
    //
    // Azure ETag concurrency protection ensures that
    // two simultaneous trader bookings cannot receive
    // the same number.

    Task<int> ReserveTraderNumberAsync(
        string eventId);
}