FROM python:3.12-alpine

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py config.py ./

RUN addgroup -S gateway && adduser -S gateway -G gateway
USER gateway

EXPOSE 5000

CMD ["python", "app.py"]
