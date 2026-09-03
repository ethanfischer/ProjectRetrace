mergeInto(LibraryManager.library, {
  RetraceDevicePixelRatio: function () {
    return window.devicePixelRatio || 1;
  }
});
