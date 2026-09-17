#include "debug_log.h"

#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

void debug_log(const char* format, ...) {
  const char* enabled = getenv("PDF_SIGNER_DEBUG");

  if (!enabled || strcmp(enabled, "1") != 0) {
    return;
  }

  va_list args;
  va_start(args, format);

  fprintf(stderr, "[pdf-signer] ");
  vfprintf(stderr, format, args);
  fprintf(stderr, "\n");

  va_end(args);
}
