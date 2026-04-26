use super::email::EmailError;

#[derive(Debug)]
pub enum UserError {
    Email(EmailError),
    EmptyPassword,
    HashingFailed,
}
