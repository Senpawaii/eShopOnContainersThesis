import re

def check_discount_from_log_file(file_path):
    pattern = r'Read Basket Service: Price {([\d.]+)}, Discount: {([\d.]+)}'
    results = {'OK': 0, 'anomalies': 0}

    with open(file_path, 'r') as file:
        for line in file:
            # Using regex, check if the line matches the pattern
            match = re.search(pattern, line)

            # If the line matches the pattern, check if the discount is 10% of the price
            if match:
                price = float(match.group(1))
                discount = float(match.group(2))
                if discount == price * 0.1:
                    results['OK'] += 1
                else:
                    results['anomalies'] += 1

    return results

# Example usage
log_file_path = "testing_scripts\\logs\\UpdatePriceAndDiscount_2023-05-16_23-20-43.log"
results = check_discount_from_log_file(log_file_path)
print(f"OK: {results['OK']}")
print(f"Anomalies: {results['anomalies']}")

# Output ratio of Anomalies to total in percentage
print(f"Anomalies ratio: {results['anomalies'] / (results['OK'] + results['anomalies']) * 100}%")