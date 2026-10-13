FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /srv

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./app

# Do not run as root.
RUN useradd --create-home --uid 10001 agent
USER agent

# Hosting platforms such as Render set PORT; default to 8001 locally.
EXPOSE 8001
CMD ["sh", "-c", "uvicorn app.main:app_factory --factory --host 0.0.0.0 --port ${PORT:-8001}"]
