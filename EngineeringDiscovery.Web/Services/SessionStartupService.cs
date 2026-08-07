using System;

namespace EngineeringDiscovery.Web.Services
{
    // Lightweight transient marker used to signal the UI to focus the composer
    // immediately after a landing-initiated session start.
    public sealed class SessionStartupService
    {
        private bool _shouldFocus;
        private string? _placeholder;

        public void MarkForFocus(string? placeholder = null)
        {
            _shouldFocus = true;
            _placeholder = placeholder;
        }

        public bool ConsumeShouldFocus()
        {
            var v = _shouldFocus;
            _shouldFocus = false;
            return v;
        }

        public string? ConsumePlaceholder()
        {
            var p = _placeholder;
            _placeholder = null;
            return p;
        }
    }
}
