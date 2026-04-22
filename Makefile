# Compiler settings
CXX = g++

# Tell the compiler exactly where the Spotify headers, third-party headers, 
# AND your local src/ headers are located
INCLUDES = -I/home/glenn/CODE/CPP/lib/spotify/SpotifyCPP/include \
           -I/home/glenn/CODE/CPP/lib/spotify/SpotifyCPP/thirdparty \
           -Isrc

# C++20 is required, plus your GTK4 UI flags and the new includes
CXXFLAGS = -std=c++23 -Wall $(INCLUDES) `pkg-config --cflags libadwaita-1 gtk4`

# Tell the linker where the compiled library file is located
LIB_PATHS = -L/home/glenn/CODE/CPP/lib/spotify/SpotifyCPP/build

# Linker flags
LDFLAGS = $(LIB_PATHS) `pkg-config --libs libadwaita-1 gtk4` -lSpotifyCPP -lcurl -lsqlite3

# ---------------------------------------------------------
# Automatically grab EVERY .cpp file inside the src/ folder
SRCS = $(wildcard src/*.cpp)

# (If your main.cpp is sitting in the root folder instead of src/, 
# comment out the line above and use this line below instead:)
# SRCS = main.cpp $(wildcard src/*.cpp)
# ---------------------------------------------------------

OBJS = $(SRCS:.cpp=.o)
TARGET = carillon

# Default build target
all: $(TARGET)

# How to link the final executable
$(TARGET): $(OBJS)
	$(CXX) $(OBJS) -o $(TARGET) $(LDFLAGS)

# How to compile individual .cpp files into .o files
%.o: %.cpp
	$(CXX) $(CXXFLAGS) -c $< -o $@

# Clean up build artifacts
clean:
	rm -f $(OBJS) $(TARGET)
