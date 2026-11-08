/* libmain.so for SDLActivity: SDL_main hands the SDL thread over to the managed game entry
 * that MainApplication registered via thor_set_main(). */
#include <stddef.h>
typedef int (*thor_main_fn)(int argc, char **argv);
static thor_main_fn g_main;
__attribute__((visibility("default"))) void thor_set_main(thor_main_fn fn) { g_main = fn; }
__attribute__((visibility("default"))) int SDL_main(int argc, char **argv) { return g_main ? g_main(argc, argv) : 1; }
