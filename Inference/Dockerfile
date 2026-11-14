# Use a lightweight official Python image
FROM python:3.11-slim

# Set the working directory inside the container
WORKDIR /app

# Install system dependencies required for OpenCV and AI model processing
RUN apt-get update && apt-get install -y \
    libgl1 \
    libglib2.0-0 \
    libegl1 \
    libgles2 \
    gcc \
    g++ \
    make \
    && rm -rf /var/lib/apt/lists/*

# Install PyTorch CPU first to avoid downloading 3GB of CUDA libraries
RUN pip install --no-cache-dir torch==2.14.1+cpu torchvision==0.29.1+cpu --index-url https://download.pytorch.org/whl/cpu

# Copy only the requirements file first to leverage Docker layer caching
COPY requirements.txt requirements.lock ./

# Install Python dependencies without caching to keep the image small
RUN pip install --no-cache-dir -r requirements.lock

# Copy the rest of the inference source code
COPY . .
RUN python download_speech_model.py
RUN python download_phoneme_model.py

# Expose the port that FastAPI will listen on
EXPOSE 8000

# Match Core's runtime UID on the shared evidence volume. Existing deployments
# must migrate volume ownership before switching to these images.
RUN groupadd --gid 1654 app && useradd --uid 1654 --gid 1654 --create-home app && \
    mkdir -p /app/state /app/images /home/app/.deepface && chown -R app:app /app /home/app
ENV HOME=/home/app
USER app

# Start the FastAPI application using Uvicorn
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
